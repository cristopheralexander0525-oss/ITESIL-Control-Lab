# 🛡️ LabControl System - ITESIL

Sistema avanzado de control y monitoreo para laboratorios de cómputo, diseñado para instituciones educativas. Permite la administración centralizada de equipos, bloqueo de seguridad, gestión de préstamos y reportes automáticos.

## 🚀 Arquitectura del Sistema

El proyecto está dividido en tres componentes principales:
1.  **LabControl.Api (Servidor):** Motor central basado en .NET 9 que gestiona la base de datos SQL Server y la comunicación en tiempo real mediante SignalR.
2.  **LabControl.Admin (Administrador):** Consola de mando para el personal técnico o docente. Controla bloqueos, apaga equipos y gestiona usuarios.
3.  **LabAgent (Agente):** Cliente de seguridad que se instala en las PCs de los alumnos. Implementa bloqueos de teclado profundos (incluyendo Win+L) y modo kiosk.

---

## ⚡ Ejecución Inmediata (Sin Visual Studio)

Si solo deseas usar el sistema sin lidiar con el código fuente, sigue estos pasos:

1.  **Descarga la última versión** desde la sección de [Releases](https://github.com/cristopheralexander0525-oss/ITESIL-Control-Lab/releases) (Descarga el archivo `Laboratorio_Portable.zip`).
2.  **Requisito de Red:** Todas las computadoras (Servidor, Admin y Agentes) **DEBEN estar conectadas a la misma red local** (ya sea por cable Ethernet o por la misma señal WiFi).
3.  **Configuración de IP de un solo clic:**
    *   En la PC Servidor, abre la carpeta `Deploy` y ejecuta `Configurar_IP_Sistema.ps1`.
    *   Este script detectará la IP de tu red actual y configurará automáticamente el Administrador y los Agentes para que todo se conecte al instante.

---

## 🛠️ Requisitos e Instalación Avanzada

*   **Runtime/SDK:** [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   **Base de Datos:** SQL Server (Express es suficiente).
*   **IDE:** Visual Studio 2022 o VS Code.

---

## 📦 Instalación y Configuración

### 1. Base de Datos
- Abre SQL Server Management Studio.
- Ejecuta el script `ITESIL_LAB_CONTROL_FULL.sql` incluido en la raíz de este repositorio.
- El servidor está configurado para conectar automáticamente a `Server=.`, por lo que funcionará en cualquier instancia local predeterminada.

### 2. Compilación
El proyecto incluye un script de automatización para generar archivos ejecutables únicos y optimizados (Self-Contained):
- Abre una terminal de PowerShell en la raíz del proyecto.
- Ejecuta: `.\compilar_final.ps1`
- Esto generará una carpeta llamada `Deploy/` con todo lo necesario.

### 3. Despliegue en el Laboratorio
Para mover el sistema a una red real:
1.  Copia la carpeta `Deploy/` al servidor del laboratorio.
2.  Haz clic derecho en `Configurar_IP_Sistema.ps1` y selecciona "Ejecutar con PowerShell". Esto actualizará las IPs de todos los componentes automáticamente.
3.  Reparte la carpeta `Agente/` a las PCs de los estudiantes.

---

## ✨ Funcionalidades Clave

*   **Bloqueo Total:** Desactiva teclado, botones de Windows y combinaciones de sistema (Win+L, Alt+Tab, etc.).
*   **Privilegios de Admin:** El Agente incluye un manifiesto de seguridad para ejecutarse con permisos elevados necesarios para el control del sistema.
*   **Comunicación Live:** Uso de SignalR para latencia cero en el envío de comandos comando (Bloquear/Apagar/Reiniciar).
*   **Reportes PDF:** Generación de historiales de uso profesional mediante QuestPDF.

---

## 📝 Notas de Desarrollo
Desarrollado como una solución robusta y portable para **ITESIL**. El sistema está diseñado para ser "Plug & Play", permitiendo cambiar de servidor o red con un solo script de configuración sin necesidad de recompilar código.
