# ========================================
# GUÍA DE DESPLIEGUE EN RED
# ========================================

## 📖 RESUMEN

Este documento explica cómo desplegar el Sistema de Control de Laboratorio ITESIL en una red con:
- **1 Servidor** (con SQL Server y Backend API)
- **N PCs de laboratorio** (con Agentes instalados)

---

## 🏗️ ARQUITECTURA EN RED

```
┌─────────────────────────────────────────┐
│  SERVIDOR (PC Principal)                │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│  • SQL Server (BD: ITESIL_LAB_CONTROL)  │
│  • Backend API (Puerto 5000)            │
│  • IP: 192.168.1.100 (ejemplo)          │
└───────────────┬─────────────────────────┘
                │
        ┌───────┴────────┐
        │   RED LOCAL    │
        └───────┬────────┘
                │
    ┌───────────┼───────────┬─────────────┐
    │           │           │             │
┌───▼───┐   ┌──▼────┐  ┌──▼────┐    ┌───▼───┐
│ PC-01 │   │ PC-02 │  │ PC-03 │ ...│ PC-30 │
│ Agente│   │ Agente│  │ Agente│    │ Agente│
└───────┘   └───────┘  └───────┘    └───────┘
```

---

## 📋 REQUISITOS

### Servidor:
- Windows Server 2016+ o Windows 10/11 Pro
- SQL Server (Express, Standard o Enterprise)
- IP fija o DHCP con reserva
- Puerto 5000 abierto en firewall

### PCs de Laboratorio:
- Windows 10/11
- Conexión de red con el servidor
- Sin requisitos adicionales (instalador autocontenido)

---

## 🚀 PASO 1: COMPILAR LOS INSTALADORES

### En tu PC de desarrollo:

#### 1.1 Compilar el Servidor
```powershell
cd C:\Users\pc\Downloads\ITESIL-Control_Lab
.\build-server.ps1
```

Generará: `Deploy\LabControl.Api\` con todos los archivos

#### 1.2 Compilar el Agente
```powershell
.\build-agent.ps1
```

Generará: `Deploy\LabAgent\` con todos los archivos

---

## 🖥️ PASO 2: INSTALAR EL SERVIDOR

### 2.1 Preparar SQL Server
1. Conéctate al servidor con SSMS
2. Ejecuta tu script SQL completo
3. Verifica que exista la BD `ITESIL_LAB_CONTROL`

### 2.2 Instalar Backend API
1. Copia la carpeta `Deploy\LabControl.Api\` al servidor
2. Ejecuta `VER_IP_SERVIDOR.bat` y anota la IP
3. Ejecuta `INSTALAR_SERVIDOR.bat` como Administrador
4. Verifica que el servicio esté corriendo en `services.msc`

### 2.3 Probar conexión
Desde el mismo servidor:
```powershell
curl http://localhost:5000/api/admin/commands -H "X-ADMIN-TOKEN: AdminDashboard_2026_Token_3a7f9e1d5c2b"
```

Deberías recibir `[]` (array vacío) o datos.

### 2.4 Configurar Firewall (si es necesario)
Si el instalador no configuró el firewall:
```powershell
netsh advfirewall firewall add rule name="ITESIL Lab" dir=in action=allow protocol=TCP localport=5000
```

---

## 💻 PASO 3: CONFIGURAR LOS AGENTES

### ⚠️ IMPORTANTE: Configurar IP del servidor

Antes de compilar los agentes, debes cambiar la IP del servidor:

1. Abre `LabAgent\Program.cs`
2. Encuentra la línea 11:
   ```csharp
   private static readonly string SERVER_URL = "http://localhost:5000/api/agent";
   ```
3. Cámbiala por la IP real del servidor:
   ```csharp
   private static readonly string SERVER_URL = "http://192.168.1.100:5000/api/agent";
   ```
   (Reemplaza `192.168.1.100` con la IP de tu servidor)

4. Vuelve a compilar:
   ```powershell
   .\build-agent.ps1
   ```

---

## 🔧 PASO 4: INSTALAR AGENTES EN PCS

### Instalación en cada PC del laboratorio:

1. Copia `Deploy\LabAgent\` a cada PC (USB, red compartida, etc.)
2. Ejecuta `INSTALAR.bat` como Administrador
3. Verifica en `services.msc` que esté corriendo
4. Espera 5 segundos

### Verificación:
En el servidor, ejecuta en SSMS:
```sql
SELECT hostname, status, last_seen 
FROM computers 
ORDER BY last_seen DESC;
```

Deberías ver todas las PCs listadas.

---

## 🧪 PASO 5: PRUEBA DE COMANDO EN RED

### Desde SQL Server (en el servidor):

```sql
-- Enviar comando a una PC específica
DECLARE @pcId UNIQUEIDENTIFIER = (
    SELECT id FROM computers WHERE hostname = 'LAB01-PC05'
);

INSERT INTO equipment_commands (id, computer_id, command, status, issued_at)
VALUES (NEWID(), @pcId, 'open notepad', 'pending', SYSDATETIME());
```

**En 5 segundos, el Notepad debe abrirse en LAB01-PC05** 🎉

---

## 📊 TOPOLOGÍA DE RED RECOMENDADA

### Opción A: Red de Laboratorio Aislada
```
Router/Switch Principal
    └─ VLAN Laboratorio (192.168.100.0/24)
        ├─ Servidor: 192.168.100.1
        ├─ PC-01: 192.168.100.10
        ├─ PC-02: 192.168.100.11
        └─ PC-XX: 192.168.100.XX
```

### Opción B: Red Corporativa
```
Red Institucional (10.50.0.0/16)
    ├─ Servidor: 10.50.10.5
    └─ Subnet Laboratorio: 10.50.20.0/24
        ├─ PC-01: 10.50.20.10
        ├─ PC-02: 10.50.20.11
        └─ ...
```

---

## 🔐 CONFIGURACIÓN DE SEGURIDAD

### Firewall del Servidor:
- Permitir entrada TCP en puerto 5000
- Solo desde la subnet del laboratorio (opcional)

### Firewall de PCs:
- No requiere configuración (agentes hacen conexiones salientes)

### SQL Server:
- Configurar autenticación Windows
- Dar permisos al usuario que ejecuta el servicio API

---

## 🛠️ SCRIPTS DE MANTENIMIENTO

### Ver estado de todos los agentes:
```sql
SELECT 
    hostname,
    status,
    last_seen,
    DATEDIFF(SECOND, last_seen, SYSDATETIME()) AS segundos_sin_reportar
FROM computers
ORDER BY last_seen DESC;
```

### Detectar agentes caídos (más de 1 minuto sin reportar):
```sql
SELECT hostname, last_seen
FROM computers
WHERE DATEDIFF(SECOND, last_seen, SYSDATETIME()) > 60;
```

### Historial de comandos por PC:
```sql
SELECT 
    c.hostname,
    ec.command,
    ec.status,
    ec.issued_at,
    ec.result
FROM equipment_commands ec
JOIN computers c ON ec.computer_id = c.id
WHERE c.hostname = 'LAB01-PC05'
ORDER BY ec.issued_at DESC;
```

---

## 📋 CHECKLIST DE DESPLIEGUE

### Servidor:
- [ ] SQL Server instalado y corriendo
- [ ] Base de datos ITESIL_LAB_CONTROL creada
- [ ] Backend API instalado como servicio
- [ ] Puerto 5000 abierto en firewall
- [ ] IP del servidor anotada
- [ ] Servicio corriendo (services.msc)

### Cada PC de Laboratorio:
- [ ] Agente compilado con IP correcta del servidor
- [ ] Carpeta copiada a la PC
- [ ] INSTALAR.bat ejecutado como Admin
- [ ] Servicio corriendo en services.msc
- [ ] PC visible en tabla `computers` (SQL)

---

## ❓ SOLUCIÓN DE PROBLEMAS EN RED

### Problema: Agente no se conecta al servidor

**Diagnóstico:**
```powershell
# En el PC del agente:
ping IP_DEL_SERVIDOR
telnet IP_DEL_SERVIDOR 5000
```

**Soluciones:**
1. Verificar conectividad de red
2. Verificar firewall del servidor
3. Verificar que el servicio API esté corriendo
4. Verificar la IP configurada en el agente

### Problema: Comando no llega a la PC

**Diagnóstico:**
```sql
-- Ver el estado del comando
SELECT * FROM equipment_commands WHERE id = 'GUID_DEL_COMANDO';
```

**Soluciones:**
1. Verificar que status sea 'pending'
2. Esperar 5 segundos (intervalo del agente)
3. Verificar que computer_id sea correcto
4. Revisar servicio del agente en la PC destino

---

## 🎯 INDICADORES DE ÉXITO

✅ Backend corriendo y accesible desde red
✅ Todas las PCs reportándose cada 5 segundos
✅ Comandos ejecutándose en menos de 5 segundos
✅ Resultados registrados en BD correctamente

---

## 📞 INFORMACIÓN TÉCNICA

**Credenciales del Sistema:**
- AGENT_API_KEY: `LabAgent_2026_Secure_Key_9f4e7d2a1b8c`
- ADMIN_TOKEN: `AdminDashboard_2026_Token_3a7f9e1d5c2b`

**Puertos:**
- Backend API: 5000 (TCP)
- SQL Server: 1433 (TCP) - solo servidor

**Servicios Windows:**
- Backend: `ITESIL_LabControlAPI`
- Agente: `ITESIL_LabAgent`

---

## 📚 DOCUMENTOS RELACIONADOS

- `GUIA_PRUEBAS.md` - Pruebas locales
- `LEEME_SERVIDOR.txt` - Instrucciones del servidor
- `LEEME.txt` - Instrucciones del agente
