<#
.SINOPSIS
    Tareas frecuentes del proyecto de la Mesa de Ayuda.

.DESCRIPCION
    Reune en un solo lugar los comandos que se usan durante la sesion, para no
    tener que recordar rutas ni banderas.

.EJEMPLO
    .\tareas.ps1 levantar
    .\tareas.ps1 probar
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('ayuda', 'restaurar', 'construir', 'probar', 'ejecutar', 'levantar', 'bajar', 'reiniciar', 'registros', 'migracion', 'limpiar')]
    [string]$Tarea = 'ayuda',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Argumentos
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$proyectoApi = Join-Path $raiz 'src\MesaAyuda.Api'

function Escribir-Titulo([string]$texto) {
    Write-Host ''
    Write-Host "== $texto ==" -ForegroundColor Cyan
}

function Confirmar-Exito([string]$descripcion) {
    if ($LASTEXITCODE -ne 0) {
        throw "$descripcion fallo con codigo $LASTEXITCODE."
    }
}

function Asegurar-ArchivoEntorno {
    $archivo = Join-Path $raiz '.env'
    if (-not (Test-Path $archivo)) {
        Copy-Item (Join-Path $raiz '.env.ejemplo') $archivo
        Write-Host "Se creo .env a partir de .env.ejemplo. Revise sus valores." -ForegroundColor Yellow
    }
}

switch ($Tarea) {

    'restaurar' {
        Escribir-Titulo 'Restaurando dependencias'
        dotnet restore
        Confirmar-Exito 'La restauracion'
    }

    'construir' {
        Escribir-Titulo 'Compilando la solucion'
        dotnet build --configuration Release
        Confirmar-Exito 'La compilacion'
    }

    'probar' {
        Escribir-Titulo 'Ejecutando las pruebas'
        dotnet test --logger "console;verbosity=normal"
        Confirmar-Exito 'La ejecucion de pruebas'
    }

    'ejecutar' {
        Escribir-Titulo 'Ejecutando la API en modo desarrollo'
        Write-Host 'Requiere PostgreSQL disponible. Para levantarlo use: .\tareas.ps1 levantar' -ForegroundColor DarkGray
        dotnet run --project $proyectoApi
    }

    'levantar' {
        Asegurar-ArchivoEntorno
        Escribir-Titulo 'Levantando los servicios con Docker'
        Write-Host 'La primera vez se descargan los modelos y puede tardar varios minutos.' -ForegroundColor DarkGray
        docker compose up --build -d
        Confirmar-Exito 'El arranque de los contenedores'

        Write-Host ''
        Write-Host 'Servicios disponibles:' -ForegroundColor Green
        Write-Host '  API ............ http://localhost:8080'
        Write-Host '  Documentacion .. http://localhost:8080/swagger'
        Write-Host '  Estado ......... http://localhost:8080/salud'
        Write-Host '  Orquestador .... http://localhost:5678'
        Write-Host '  Correo ......... http://localhost:8025'
        Write-Host ''
        Write-Host 'El servicio de modelos no publica puertos: solo la API lo alcanza.' -ForegroundColor DarkGray
    }

    'bajar' {
        Escribir-Titulo 'Deteniendo los servicios'
        docker compose down
        Confirmar-Exito 'La detencion de los contenedores'
    }

    'reiniciar' {
        Escribir-Titulo 'Reiniciando desde cero (se borran los datos)'
        docker compose down -v
        Write-Host 'Se conservan los modelos ya descargados; solo se borran los datos.' -ForegroundColor DarkGray
        docker compose up --build -d
        Confirmar-Exito 'El reinicio'
    }

    'registros' {
        Escribir-Titulo 'Registros de la API'
        docker compose logs -f api
    }

    'migracion' {
        if (-not $Argumentos -or [string]::IsNullOrWhiteSpace($Argumentos[0])) {
            throw "Indique el nombre de la migracion. Ejemplo: .\tareas.ps1 migracion AgregarCampoX"
        }

        Escribir-Titulo "Creando la migracion $($Argumentos[0])"
        dotnet ef migrations add $Argumentos[0] --project $proyectoApi --output-dir Nucleo/Datos/Migraciones
        Confirmar-Exito 'La creacion de la migracion'
    }

    'limpiar' {
        Escribir-Titulo 'Limpiando artefactos de compilacion'
        Get-ChildItem -Path $raiz -Include bin, obj -Recurse -Directory |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
        Write-Host 'Listo.' -ForegroundColor Green
    }

    default {
        Write-Host ''
        Write-Host 'Tareas disponibles' -ForegroundColor Cyan
        Write-Host ''
        Write-Host '  restaurar   Descarga las dependencias del proyecto.'
        Write-Host '  construir   Compila la solucion en configuracion Release.'
        Write-Host '  probar      Ejecuta la bateria de pruebas automatizadas.'
        Write-Host '  ejecutar    Levanta la API directamente con dotnet run.'
        Write-Host '  levantar    Levanta API y base de datos con Docker.'
        Write-Host '  bajar       Detiene los contenedores.'
        Write-Host '  reiniciar   Rehace los contenedores y borra los datos.'
        Write-Host '  registros   Muestra los registros de la API en vivo.'
        Write-Host '  migracion   Crea una migracion. Requiere un nombre.'
        Write-Host '  limpiar     Elimina las carpetas bin y obj.'
        Write-Host ''
        Write-Host 'Ejemplo: .\tareas.ps1 levantar' -ForegroundColor DarkGray
        Write-Host ''
    }
}
