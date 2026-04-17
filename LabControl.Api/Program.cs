using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using BCrypt.Net;

var builder = WebApplication.CreateBuilder(args);

// Configurar para escuchar en todas las interfaces de red (LAN)
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// ============================
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.MapHub<LabHub>("/labHub");

// 1) Cadena de conexión a SQL Server (el punto '.' significa 'esta computadora')
string CONNECTION_STRING = "Server=.;Database=ITESIL_LAB_CONTROL;Integrated Security=true;TrustServerCertificate=True;";
// 2) API key de Agentes
string AGENT_API_KEY = "LabAgent_2026_Secure_Key_9f4e7d2a1b8c";
// 3) Token legacy (opcional, para transición)
string ADMIN_TOKEN = "AdminDashboard_2026_Token_3a7f9e1d5c2b";

// Helper para validar Sesiones de Base de Datos
async Task<(bool IsValid, string? Role, Guid? UserId)> ValidateSession(HttpRequest req)
{
    var authHeader = req.Headers["Authorization"].ToString();
    if (string.IsNullOrWhiteSpace(authHeader) && req.Headers.TryGetValue("X-ADMIN-TOKEN", out var adm) && adm == ADMIN_TOKEN)
        return (true, "admin", null); // Legacy fallback

    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ")) return (false, null, null);
    
    var token = authHeader.Substring(7);
    using var conn = new SqlConnection(CONNECTION_STRING);
    await conn.OpenAsync();
    
    var sql = @"SELECT TOP 1 u.id, r.name as RoleName 
                FROM sessions s 
                JOIN users u ON s.user_id = u.id 
                LEFT JOIN user_roles ur ON u.id = ur.user_id 
                LEFT JOIN roles r ON ur.role_id = r.id
                WHERE s.session_token = @Token AND s.expires_at > SYSUTCDATETIME() AND u.is_active = 1";
                
    var result = await conn.QueryFirstOrDefaultAsync<UserSessionData>(sql, new { Token = token });
    if (result == null) return (false, null, null);
    
    return (true, result.RoleName?.ToLower(), result.id);
}

bool IsAdmin(string? role) => role == "admin" || role == "administrador";
bool IsTechOrAdmin(string? role) => role == "admin" || role == "administrador" || role == "technician";

// ============================
// AUTHENTICATION
// ============================
app.MapPost("/api/auth/login", async (HttpRequest req) =>
{
    var payload = await req.ReadFromJsonAsync<LoginDto>();
    if (payload == null || string.IsNullOrWhiteSpace(payload.Username) || string.IsNullOrWhiteSpace(payload.Password))
        return Results.BadRequest(new { error = "Usuario y contraseña requeridos" });

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();

        var sql = @"SELECT u.id, u.username, u.password_hash, u.is_active, r.name as RoleName
                    FROM users u
                    LEFT JOIN user_roles ur ON u.id = ur.user_id
                    LEFT JOIN roles r ON ur.role_id = r.id
                    WHERE u.username = @Username OR u.email = @Username";
        
        var user = await conn.QueryFirstOrDefaultAsync<UserRow>(sql, new { Username = payload.Username });

        if (user == null || user.is_active != true)
            return Results.Unauthorized();

        bool isPassValid = false;
        try { isPassValid = BCrypt.Net.BCrypt.Verify(payload.Password, user.password_hash); } catch { }

        if (!isPassValid)
            return Results.Unauthorized();

        // Generar Token en BD
        var token = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddHours(12);
        
        var sessionSql = "INSERT INTO sessions (id, user_id, session_token, created_at, expires_at) VALUES (NEWID(), @UserId, @Token, SYSUTCDATETIME(), @Expires)";
        await conn.ExecuteAsync(sessionSql, new { UserId = user.id, Token = token, Expires = expires });

        return Results.Ok(new {
            token = token,
            username = user.username,
            role = user.RoleName,
            isAdmin = user.RoleName?.ToLower() == "admin",
            isTechnician = user.RoleName?.ToLower() == "technician",
            isStudent = user.RoleName?.ToLower() == "student"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
});

app.MapPost("/api/auth/logout", async (HttpRequest req) =>
{
    var authHeader = req.Headers["Authorization"].ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ")) return Results.Ok();
    var token = authHeader.Substring(7);
    
    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        // Borrar o expirar sesión
        await conn.ExecuteAsync("DELETE FROM sessions WHERE session_token = @Token", new { Token = token });
        return Results.Ok();
    } catch { return Results.Ok(); }
});

// ============================
// ENDPOINTS DEL AGENTE
// ============================
app.MapPost("/api/agent/checkin", async (HttpRequest req, IHubContext<LabHub> hubContext) =>
{
    var payload = await req.ReadFromJsonAsync<CheckInDto>();
    if (payload == null || string.IsNullOrWhiteSpace(payload.Hostname))
        return Results.BadRequest("Invalid payload");

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();

        var sql = "UPDATE dbo.computers SET last_seen_at = SYSUTCDATETIME() WHERE hostname = @Hostname;";
        var affected = await conn.ExecuteAsync(sql, new { Hostname = payload.Hostname });

        if (affected == 0)
        {
            var insertSql = "INSERT INTO dbo.computers (id, hostname, status, last_seen_at) VALUES (NEWID(), @Hostname, 'available', SYSUTCDATETIME());";
            await conn.ExecuteAsync(insertSql, new { Hostname = payload.Hostname });
        }

        await hubContext.Clients.Group("ADMINS").SendAsync("PCStatusChanged");
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapGet("/api/agent/commands/{hostname}", async (HttpRequest req, string hostname) =>
{
    if (!req.Headers.TryGetValue("X-API-KEY", out var key) || key != AGENT_API_KEY) return Results.Unauthorized();

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();

        var compId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM dbo.computers WHERE hostname = @Hostname", new { Hostname = hostname });
        if (compId == null) return Results.Ok();

        var command = await conn.QueryFirstOrDefaultAsync<CommandRow>(
            "SELECT TOP 1 id, command FROM dbo.equipment_commands WHERE computer_id = @ComputerId AND status = 'pending' ORDER BY issued_at ASC;", 
            new { ComputerId = compId });
            
        if (command == null) return Results.Ok();

        await conn.ExecuteAsync("UPDATE dbo.equipment_commands SET status = 'processing' WHERE id = @Id;", new { Id = command.id });
        return Results.Json(new { id = command.id.ToString(), command = command.command });
    }
    catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapPost("/api/agent/result", async (HttpRequest req) =>
{
    if (!req.Headers.TryGetValue("X-API-KEY", out var key) || key != AGENT_API_KEY) return Results.Unauthorized();
    var payload = await req.ReadFromJsonAsync<CommandResultDto>();
    if (payload == null) return Results.BadRequest("Invalid payload");

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var resJson = payload.Result ?? $"{{\"executed_at\":\"{DateTime.UtcNow:O}\",\"status\":\"{payload.Status}\"}}";
        await conn.ExecuteAsync("UPDATE dbo.equipment_commands SET status = @Status, result = @Result WHERE id = @Id;", 
            new { Status = payload.Status, Result = resJson, Id = Guid.Parse(payload.CommandId) });
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

// ============================
// PASSWORD RESET / USUARIOS (Admin)
// ============================
app.MapPost("/api/auth/request-reset", async (HttpRequest req) =>
{
    var payload = await req.ReadFromJsonAsync<ResetRequestDto>();
    if (payload == null || string.IsNullOrWhiteSpace(payload.Username) || string.IsNullOrWhiteSpace(payload.NewPassword)) return Results.BadRequest();
    
    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        
        // Hashea la contraseña temporalmente (el admin decide si la aplica)
        var hash = BCrypt.Net.BCrypt.HashPassword(payload.NewPassword);
        
        await conn.ExecuteAsync(
            "INSERT INTO password_reset_requests (username, requested_password) VALUES (@User, @Hash)", 
            new { User = payload.Username, Hash = hash });
            
        return Results.Ok();
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapGet("/api/admin/reset-requests", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || (role != "Administrador" && role != "admin")) return Results.Unauthorized();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync("SELECT id, username, created_at, status FROM password_reset_requests WHERE status = 'pending' ORDER BY created_at DESC");
        return Results.Json(rows);
    } catch { return Results.Problem(); }
});

app.MapPost("/api/admin/resolve-reset", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || (role != "Administrador" && role != "admin")) return Results.Unauthorized();
    
    var payload = await req.ReadFromJsonAsync<ResolveResetDto>();
    if (payload == null) return Results.BadRequest();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        
        var request = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT username, requested_password FROM password_reset_requests WHERE id = @Id AND status = 'pending'", new { Id = payload.RequestId });
        if (request == null) return Results.NotFound();

        if (payload.Approve) {
            // Aplicar el cambio a la tabla de usuarios
            await conn.ExecuteAsync("UPDATE users SET password_hash = @Hash WHERE username = @User", new { Hash = request.requested_password, User = request.username });
            await conn.ExecuteAsync("UPDATE password_reset_requests SET status = 'approved', resolved_at = SYSUTCDATETIME() WHERE id = @Id", new { Id = payload.RequestId });
        } else {
            await conn.ExecuteAsync("UPDATE password_reset_requests SET status = 'rejected', resolved_at = SYSUTCDATETIME() WHERE id = @Id", new { Id = payload.RequestId });
        }
        return Results.Ok();
    } catch { return Results.Problem(); }
});

// ============================
// ENDPOINTS ADMIN (Protegidos)
// ============================
app.MapPost("/api/admin/command", async (HttpRequest req, IHubContext<LabHub> hubContext) =>
{
    var (isValid, role, userId) = await ValidateSession(req);
    if (!isValid || role == "Alumno") return Results.Unauthorized(); // Solo Admin y Docente pueden enviar comandos

    var payload = await req.ReadFromJsonAsync<AdminCreateCommandDto>();
    if (payload == null) return Results.BadRequest("Invalid payload");

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();

        var compId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM dbo.computers WHERE hostname = @Hostname", new { Hostname = payload.Hostname });
        if (compId == null) return Results.BadRequest("Computer not found");

        var commandId = Guid.NewGuid();
        await conn.ExecuteAsync(
            "INSERT INTO dbo.equipment_commands (id, computer_id, command, issued_at, status, issued_by) VALUES (@Id, @ComputerId, @Command, SYSUTCDATETIME(), 'pending', @UserId);", 
            new { Id = commandId, ComputerId = compId, Command = payload.Command, UserId = userId });

        await hubContext.Clients.Group(payload.Hostname).SendAsync("ReceiveCommand", new { id = commandId.ToString(), command = payload.Command });
        return Results.Ok(new { id = commandId.ToString(), hostname = payload.Hostname, command = payload.Command });
    }
    catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapGet("/api/admin/computers", async (HttpRequest req) =>
{
    var (isValid, _, _) = await ValidateSession(req);
    if (!isValid) return Results.Unauthorized();

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();

        var rows = await conn.QueryAsync(@"
            SELECT id, hostname, last_seen_at, 
                   CASE WHEN DATEDIFF(SECOND, last_seen_at, SYSUTCDATETIME()) < 15 THEN 'online' ELSE 'offline' END as status
            FROM dbo.computers ORDER BY hostname ASC;");
        return Results.Json(rows);
    }
    catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapDelete("/api/admin/computers/{hostname}", async (HttpRequest req, string hostname) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    // Solo administrador debe poder eliminar equipos del registro.
    if (!isValid || (role != "Administrador" && role != "admin")) return Results.Unauthorized();

    try
    {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        
        // Ensure reservations referentially integrity or delete them if cascade is not enabled
        await conn.ExecuteAsync("DELETE FROM reservations WHERE computer_id IN (SELECT id FROM computers WHERE hostname = @Hostname)", new { Hostname = hostname });
        await conn.ExecuteAsync("DELETE FROM equipment_commands WHERE computer_id IN (SELECT id FROM computers WHERE hostname = @Hostname)", new { Hostname = hostname });

        var affected = await conn.ExecuteAsync("DELETE FROM dbo.computers WHERE hostname = @Hostname", new { Hostname = hostname });
        
        if (affected == 0) return Results.NotFound(new { error = "Equipo no encontrado" });
        return Results.Ok();
    }
    catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

// ============================

// LOANS / RESERVATIONS
// ============================
app.MapGet("/api/admin/loans", async (HttpRequest req) =>
{
    var (isValid, _, _) = await ValidateSession(req);
    if (!isValid) return Results.Unauthorized();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync(@"
            SELECT r.id, u.username, c.hostname, r.purpose, r.start_at, r.end_at, r.returned_at, r.status
            FROM reservations r
            LEFT JOIN users u ON r.user_id = u.id
            LEFT JOIN computers c ON r.computer_id = c.id
            ORDER BY r.created_at DESC;");
        return Results.Json(rows);
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapPost("/api/admin/loans", async (HttpRequest req) =>
{
    var (isValid, role, userId) = await ValidateSession(req);
    if (!isValid || role == "student") return Results.Unauthorized();

    var payload = await req.ReadFromJsonAsync<CreateLoanDto>();
    if (payload == null) return Results.BadRequest();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();

        var targetUserId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM users WHERE username = @Username", new { Username = payload.Username });
        if (targetUserId == null) return Results.BadRequest(new { error = "Usuario no encontrado" });

        var computerId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM computers WHERE hostname = @Hostname", new { Hostname = payload.Hostname });
        
        // Si es el Laboratorio Completo y no existe en la tabla computers, lo creamos como entidad virtual
        if (computerId == null && payload.Hostname == "Laboratorio Completo")
        {
            computerId = Guid.NewGuid();
            await conn.ExecuteAsync("INSERT INTO computers (id, hostname, status) VALUES (@Id, @Hostname, 'available')", new { Id = computerId, Hostname = "Laboratorio Completo" });
        }

        if (computerId == null) return Results.BadRequest(new { error = "Equipo no encontrado" });

        await conn.ExecuteAsync(@"
            INSERT INTO reservations (id, user_id, computer_id, purpose, start_at, end_at, status, created_at)
            VALUES (NEWID(), @UserId, @CompId, @Purpose, @StartAt, @EndAt, 'active', SYSUTCDATETIME())",
            new { UserId = targetUserId, CompId = computerId, Purpose = payload.Purpose, StartAt = payload.StartAt, EndAt = payload.EndAt });

        return Results.Ok();
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapPost("/api/admin/loans/return", async (HttpRequest req) =>
{
    var (isValid, _, _) = await ValidateSession(req);
    if (!isValid) return Results.Unauthorized();

    var payload = await req.ReadFromJsonAsync<ReturnLoanDto>();
    if (payload == null) return Results.BadRequest();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE reservations SET status = 'completed', returned_at = SYSUTCDATETIME() WHERE id = @Id", new { Id = Guid.Parse(payload.ReservationId) });
        return Results.Ok();
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});
// ============================
// CRUD DE USUARIOS (Admin)
// ============================
app.MapGet("/api/admin/roles", async (HttpRequest req) =>
{
    var (isValid, _, _) = await ValidateSession(req);
    if (!isValid) return Results.Unauthorized();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        // students are not manageable from admin console
        var rows = await conn.QueryAsync("SELECT id, name FROM roles WHERE name != 'student' ORDER BY name");
        return Results.Json(rows);
    } catch { return Results.Problem(); }
});

app.MapGet("/api/admin/users", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || !IsAdmin(role)) return Results.Unauthorized();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync(@"
            SELECT u.id, u.username, u.email, u.full_name, u.is_active, r.name as role 
            FROM users u 
            LEFT JOIN user_roles ur ON u.id = ur.user_id 
            LEFT JOIN roles r ON ur.role_id = r.id 
            ORDER BY u.created_at DESC");
        return Results.Json(rows);
    } catch { return Results.Problem(); }
});

app.MapPost("/api/admin/users", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || !IsAdmin(role)) return Results.Unauthorized();

    var payload = await req.ReadFromJsonAsync<CreateUserDto>();
    if (payload == null) return Results.BadRequest();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        
        var existing = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM users WHERE username = @User OR email = @Email", new { User = payload.Username, Email = payload.Email });
        if (existing != null) return Results.BadRequest(new { error = "El usuario o email ya existe" });

        var userId = Guid.NewGuid();
        var hash = BCrypt.Net.BCrypt.HashPassword(payload.Password);
        
        await conn.ExecuteAsync("INSERT INTO users (id, username, email, full_name, password_hash, is_active) VALUES (@Id, @User, @Email, @Name, @Hash, @IsActive)", 
            new { Id = userId, User = payload.Username, Email = payload.Email, Name = payload.FullName, Hash = hash, IsActive = payload.IsActive ? 1 : 0 });
            
        if (!string.IsNullOrEmpty(payload.Role)) {
            var roleId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM roles WHERE name = @Role", new { Role = payload.Role });
            if (roleId != null) {
                await conn.ExecuteAsync("INSERT INTO user_roles (user_id, role_id) VALUES (@Uid, @Rid)", new { Uid = userId, Rid = roleId });
            }
        }
        
        return Results.Ok();
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapPut("/api/admin/users/{id}", async (HttpRequest req, string id) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || !IsAdmin(role)) return Results.Unauthorized();

    var payload = await req.ReadFromJsonAsync<UpdateUserDto>();
    if (payload == null) return Results.BadRequest();

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var uid = Guid.Parse(id);
        
        await conn.ExecuteAsync("UPDATE users SET email = @Email, full_name = @Name, is_active = @IsActive WHERE id = @Id", 
            new { Email = payload.Email, Name = payload.FullName, IsActive = payload.IsActive ? 1 : 0, Id = uid });

        if (!string.IsNullOrWhiteSpace(payload.Password)) {
            var hash = BCrypt.Net.BCrypt.HashPassword(payload.Password);
            await conn.ExecuteAsync("UPDATE users SET password_hash = @Hash WHERE id = @Id", new { Hash = hash, Id = uid });
        }
            
        if (!string.IsNullOrEmpty(payload.Role)) {
            await conn.ExecuteAsync("DELETE FROM user_roles WHERE user_id = @Uid", new { Uid = uid });
            var roleId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM roles WHERE name = @Role", new { Role = payload.Role });
            if (roleId != null) {
                await conn.ExecuteAsync("INSERT INTO user_roles (user_id, role_id) VALUES (@Uid, @Rid)", new { Uid = uid, Rid = roleId });
            }
        }
        
        return Results.Ok();
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

// ============================
// REPORTES
// ============================
app.MapGet("/api/admin/reports/reservations", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || !IsAdmin(role)) return Results.Unauthorized();

    string? from   = req.Query["from"];
    string? to     = req.Query["to"];
    string? status = req.Query["status"];

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var where = new System.Text.StringBuilder("WHERE 1=1");
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(from))   { where.Append(" AND r.start_at >= @From");  p.Add("From", DateTime.Parse(from)); }
        if (!string.IsNullOrEmpty(to))     { where.Append(" AND r.end_at   <= @To");    p.Add("To",   DateTime.Parse(to).AddDays(1)); }
        if (!string.IsNullOrEmpty(status) && status != "all") { where.Append(" AND r.status = @Status"); p.Add("Status", status); }
        var sql = $@"SELECT r.id, u.username, u.full_name, c.hostname, r.purpose,
                            r.start_at, r.end_at, r.returned_at, r.status, r.created_at
                     FROM reservations r
                     JOIN users u ON r.user_id = u.id
                     JOIN computers c ON r.computer_id = c.id
                     {where}
                     ORDER BY r.created_at DESC";
        var rows = await conn.QueryAsync(sql, p);
        return Results.Json(rows);
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapGet("/api/admin/reports/equipment-usage", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || !IsAdmin(role)) return Results.Unauthorized();

    string? from = req.Query["from"];
    string? to   = req.Query["to"];

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var joinWhere = new System.Text.StringBuilder("AND 1=1");
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(from)) { joinWhere.Append(" AND r.start_at >= @From"); p.Add("From", DateTime.Parse(from)); }
        if (!string.IsNullOrEmpty(to))   { joinWhere.Append(" AND r.end_at   <= @To");   p.Add("To",   DateTime.Parse(to).AddDays(1)); }
        var sql = $@"SELECT c.hostname,
                            COUNT(r.id) AS total_reservas,
                            SUM(DATEDIFF(MINUTE, r.start_at, ISNULL(r.returned_at, r.end_at))) AS minutos_uso,
                            COUNT(CASE WHEN r.status='completed' THEN 1 END) AS completadas,
                            COUNT(CASE WHEN r.status='active'    THEN 1 END) AS activas
                     FROM computers c
                     LEFT JOIN reservations r ON c.id = r.computer_id {joinWhere}
                     GROUP BY c.hostname
                     ORDER BY total_reservas DESC";
        var rows = await conn.QueryAsync(sql, p);
        return Results.Json(rows);
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.MapGet("/api/admin/reports/user-activity", async (HttpRequest req) =>
{
    var (isValid, role, _) = await ValidateSession(req);
    if (!isValid || !IsAdmin(role)) return Results.Unauthorized();

    string? from = req.Query["from"];
    string? to   = req.Query["to"];

    try {
        using var conn = new SqlConnection(CONNECTION_STRING);
        await conn.OpenAsync();
        var joinWhere = new System.Text.StringBuilder("AND 1=1");
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(from)) { joinWhere.Append(" AND r.start_at >= @From"); p.Add("From", DateTime.Parse(from)); }
        if (!string.IsNullOrEmpty(to))   { joinWhere.Append(" AND r.end_at   <= @To");   p.Add("To",   DateTime.Parse(to).AddDays(1)); }
        var sql = $@"SELECT u.username, u.full_name, ro.name AS role,
                            COUNT(r.id) AS total_reservas,
                            COUNT(CASE WHEN r.status='completed' THEN 1 END) AS completadas,
                            COUNT(CASE WHEN r.status='active'    THEN 1 END) AS activas,
                            MAX(r.created_at) AS ultima_actividad
                     FROM users u
                     LEFT JOIN user_roles ur ON u.id = ur.user_id
                     LEFT JOIN roles ro ON ur.role_id = ro.id
                     LEFT JOIN reservations r ON u.id = r.user_id {joinWhere}
                     GROUP BY u.username, u.full_name, ro.name
                     ORDER BY total_reservas DESC";
        var rows = await conn.QueryAsync(sql, p);
        return Results.Json(rows);
    } catch (Exception ex) { return Results.Problem(detail: ex.Message); }
});

app.Run();

// ============================
// CLASES Y HUB
// ============================
public class LabHub : Hub
{
    public async Task Register(string hostname)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, hostname);
        if (hostname == "ADMIN_CONSOLE") await Groups.AddToGroupAsync(Context.ConnectionId, "ADMINS");
        Console.WriteLine($"[SignalR] {hostname} conectado");
    }
}

public record CheckInDto([property: JsonPropertyName("hostname")] string Hostname, [property: JsonPropertyName("last_seen_at")] DateTime? LastSeenAt);
public record CommandResultDto([property: JsonPropertyName("command_id")] string CommandId, [property: JsonPropertyName("status")] string Status, [property: JsonPropertyName("result")] string? Result);
public record AdminCreateCommandDto([property: JsonPropertyName("hostname")] string Hostname, [property: JsonPropertyName("command")] string Command);
public record LoginDto([property: JsonPropertyName("username")] string Username, [property: JsonPropertyName("password")] string Password);

public record ResetRequestDto([property: JsonPropertyName("username")] string Username, [property: JsonPropertyName("newPassword")] string NewPassword);
public record ResolveResetDto([property: JsonPropertyName("requestId")] string RequestId, [property: JsonPropertyName("approve")] bool Approve);
public record CreateLoanDto([property: JsonPropertyName("username")] string Username, [property: JsonPropertyName("hostname")] string Hostname, [property: JsonPropertyName("purpose")] string Purpose, [property: JsonPropertyName("startAt")] DateTime StartAt, [property: JsonPropertyName("endAt")] DateTime EndAt);
public record ReturnLoanDto([property: JsonPropertyName("reservationId")] string ReservationId);
public record CreateUserDto([property: JsonPropertyName("username")] string Username, [property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("password")] string Password, [property: JsonPropertyName("fullName")] string FullName, [property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("isActive")] bool IsActive = true);
public record UpdateUserDto([property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("password")] string? Password, [property: JsonPropertyName("fullName")] string FullName, [property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("isActive")] bool IsActive);

public class CommandRow { public Guid id { get; set; } public string command { get; set; } = ""; }
public class ComputerDto { public Guid id { get; set; } public string hostname { get; set; } = ""; public string Status { get; set; } = ""; }
public class UserSessionData { public Guid id { get; set; } public string RoleName { get; set; } = ""; }
public class UserRow { public Guid id { get; set; } public string username { get; set; } = ""; public string password_hash { get; set; } = ""; public bool? is_active { get; set; } public string RoleName { get; set; } = ""; }
