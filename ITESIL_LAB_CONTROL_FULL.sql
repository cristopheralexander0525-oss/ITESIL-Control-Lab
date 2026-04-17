/********************************************************************
 SISTEMA DE GESTIÓN Y CONTROL DE LABORATORIOS - ITESIL
 SQL Server / SSMS
 Script COMPLETO (BD + Seguridad + Automatización + Dashboard)

 ✅ COMPATIBLE CON LabControl.Api (Backend .NET)
********************************************************************/

-- =====================================================
-- 1. BASE DE DATOS
-- =====================================================
IF DB_ID('ITESIL_LAB_CONTROL') IS NULL
    CREATE DATABASE ITESIL_LAB_CONTROL;
GO

USE ITESIL_LAB_CONTROL;
GO

-- =====================================================
-- 2. LIMPIEZA (DEV)
-- =====================================================
IF OBJECT_ID('equipment_commands') IS NOT NULL DROP TABLE equipment_commands;
IF OBJECT_ID('access_logs') IS NOT NULL DROP TABLE access_logs;
IF OBJECT_ID('incidents') IS NOT NULL DROP TABLE incidents;
IF OBJECT_ID('password_resets') IS NOT NULL DROP TABLE password_resets;
IF OBJECT_ID('sessions') IS NOT NULL DROP TABLE sessions;
IF OBJECT_ID('reservations') IS NOT NULL DROP TABLE reservations;
IF OBJECT_ID('computers') IS NOT NULL DROP TABLE computers;
IF OBJECT_ID('equipment_types') IS NOT NULL DROP TABLE equipment_types;
IF OBJECT_ID('labs') IS NOT NULL DROP TABLE labs;
IF OBJECT_ID('user_roles') IS NOT NULL DROP TABLE user_roles;
IF OBJECT_ID('roles') IS NOT NULL DROP TABLE roles;
IF OBJECT_ID('users') IS NOT NULL DROP TABLE users;
GO

-- =====================================================
-- 3. USUARIOS Y ROLES
-- =====================================================
CREATE TABLE users (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    username NVARCHAR(100) UNIQUE NOT NULL,
    email NVARCHAR(200) UNIQUE NOT NULL,
    password_hash NVARCHAR(500) NOT NULL,
    full_name NVARCHAR(200),
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    last_login DATETIME2
);

CREATE TABLE roles (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name NVARCHAR(50) UNIQUE NOT NULL,
    description NVARCHAR(200)
);

CREATE TABLE user_roles (
    user_id UNIQUEIDENTIFIER,
    role_id UNIQUEIDENTIFIER,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
);
GO

-- =====================================================
-- 4. LABORATORIOS Y EQUIPOS
-- =====================================================
CREATE TABLE labs (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    code NVARCHAR(50) UNIQUE,
    name NVARCHAR(200),
    location NVARCHAR(200),
    capacity INT
);

CREATE TABLE equipment_types (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name NVARCHAR(100)
);

CREATE TABLE computers (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    lab_id UNIQUEIDENTIFIER,
    hostname NVARCHAR(150) UNIQUE,
    asset_tag NVARCHAR(100),
    equipment_type_id UNIQUEIDENTIFIER,
    status NVARCHAR(20) DEFAULT 'available',
    last_seen_at DATETIME2,
    notes NVARCHAR(MAX),
    FOREIGN KEY (lab_id) REFERENCES labs(id),
    FOREIGN KEY (equipment_type_id) REFERENCES equipment_types(id),
    CHECK (status IN ('available','in_use','maintenance','offline'))
);
GO

-- =====================================================
-- 5. RESERVAS
-- =====================================================
CREATE TABLE reservations (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    user_id UNIQUEIDENTIFIER,
    computer_id UNIQUEIDENTIFIER,
    purpose NVARCHAR(300),
    start_at DATETIME2,
    end_at DATETIME2,
    returned_at DATETIME2,
    status NVARCHAR(20) DEFAULT 'reserved',
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (computer_id) REFERENCES computers(id),
    CHECK (status IN ('reserved','active','completed','cancelled','overdue')),
    CHECK (end_at > start_at)
);
GO

-- =====================================================
-- 6. SEGURIDAD
-- =====================================================
CREATE TABLE sessions (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    user_id UNIQUEIDENTIFIER,
    session_token NVARCHAR(200),
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    expires_at DATETIME2,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE password_resets (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    user_id UNIQUEIDENTIFIER,
    token NVARCHAR(200),
    expires_at DATETIME2,
    FOREIGN KEY (user_id) REFERENCES users(id)
);
GO

-- =====================================================
-- 7. AUDITORÍA
-- =====================================================
CREATE TABLE access_logs (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    user_id UNIQUEIDENTIFIER,
    action NVARCHAR(200),
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    details NVARCHAR(MAX)
);

CREATE TABLE incidents (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    reported_by UNIQUEIDENTIFIER,
    computer_id UNIQUEIDENTIFIER,
    title NVARCHAR(200),
    description NVARCHAR(MAX),
    severity NVARCHAR(20),
    status NVARCHAR(20),
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (reported_by) REFERENCES users(id),
    FOREIGN KEY (computer_id) REFERENCES computers(id)
);
GO

-- =====================================================
-- 8. CONTROL REMOTO
-- =====================================================
CREATE TABLE equipment_commands (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    computer_id UNIQUEIDENTIFIER,
    command NVARCHAR(300),
    issued_by UNIQUEIDENTIFIER,
    issued_at DATETIME2 DEFAULT SYSDATETIME(),
    status NVARCHAR(20),
    result NVARCHAR(MAX),
    FOREIGN KEY (computer_id) REFERENCES computers(id),
    FOREIGN KEY (issued_by) REFERENCES users(id)
);
GO

-- =====================================================
-- 9. TRIGGER (DEBE IR SOLO EN EL BATCH)
-- =====================================================
GO
CREATE TRIGGER trg_reservation_status
ON reservations
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
    SET c.status = 'in_use'
    FROM computers c
    JOIN inserted i ON c.id = i.computer_id
    WHERE i.status = 'active';

    UPDATE c
    SET c.status = 'available'
    FROM computers c
    JOIN inserted i ON c.id = i.computer_id
    WHERE i.status IN ('completed','cancelled');
END;
GO

-- =====================================================
-- 10. VISTA DASHBOARD (TABLAS YA EXISTEN)
-- =====================================================
GO
CREATE VIEW vw_lab_status AS
SELECT
    l.name,
    COUNT(c.id) AS total,
    SUM(CASE WHEN c.status = 'available' THEN 1 ELSE 0 END) AS available
FROM labs l
LEFT JOIN computers c ON l.id = c.lab_id
GROUP BY l.name;
GO

-- =====================================================
-- 11. DATOS DE PRUEBA (SIN VARIABLES, IDPOTENTE)
-- =====================================================
INSERT INTO equipment_types (name)
SELECT 'Desktop PC' WHERE NOT EXISTS (SELECT 1 FROM equipment_types WHERE name='Desktop PC');
INSERT INTO equipment_types (name)
SELECT 'Laptop' WHERE NOT EXISTS (SELECT 1 FROM equipment_types WHERE name='Laptop');
INSERT INTO equipment_types (name)
SELECT 'Workstation' WHERE NOT EXISTS (SELECT 1 FROM equipment_types WHERE name='Workstation');

INSERT INTO roles (name, description)
SELECT 'admin','Administrador del sistema'
WHERE NOT EXISTS (SELECT 1 FROM roles WHERE name='admin');

INSERT INTO roles (name, description)
SELECT 'technician','Técnico de laboratorio'
WHERE NOT EXISTS (SELECT 1 FROM roles WHERE name='technician');

INSERT INTO roles (name, description)
SELECT 'student','Estudiante'
WHERE NOT EXISTS (SELECT 1 FROM roles WHERE name='student');

INSERT INTO users (username, email, password_hash, full_name)
SELECT 'admin','admin@itesil.edu','HASH_PLACEHOLDER','Administrador Sistema'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE username='admin');

INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u
JOIN roles r ON r.name='admin'
WHERE u.username='admin'
AND NOT EXISTS (
    SELECT 1 FROM user_roles ur
    WHERE ur.user_id=u.id AND ur.role_id=r.id
);

-- =====================================================
-- FIN
-- =====================================================
PRINT 'Base de datos ITESIL_LAB_CONTROL creada correctamente';
PRINT 'Script ejecutado sin errores';
GO