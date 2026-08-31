# Etapa 1: compilacion de la interfaz web
#
# Ocurre dentro de la imagen para que quien solo ejecute "docker compose up" no
# necesite instalar Node. Solo hace falta tenerlo instalado para trabajar con
# recarga en caliente durante el desarrollo.
FROM node:22-alpine AS cliente
WORKDIR /origen

# Se copian primero los manifiestos para aprovechar la cache de capas: mientras
# las dependencias no cambien, no se vuelven a descargar.
COPY cliente/package.json cliente/package-lock.json cliente/
RUN cd cliente && npm ci

COPY cliente/ cliente/
RUN cd cliente && npm run build

# Etapa 2: compilacion de la API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /origen

COPY src/MesaAyuda.Api/MesaAyuda.Api.csproj src/MesaAyuda.Api/
RUN dotnet restore src/MesaAyuda.Api/MesaAyuda.Api.csproj

COPY src/ src/

# La interfaz ya compilada se incorpora a wwwroot, desde donde ASP.NET la sirve.
COPY --from=cliente /origen/src/MesaAyuda.Api/wwwroot src/MesaAyuda.Api/wwwroot

RUN dotnet publish src/MesaAyuda.Api/MesaAyuda.Api.csproj -c Release -o /publicacion --no-restore

# Etapa 3: ejecucion
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS ejecucion
WORKDIR /aplicacion

# El proceso no corre como root.
RUN adduser --disabled-password --gecos "" --uid 5000 mesa
USER mesa

COPY --from=compilacion --chown=mesa:mesa /publicacion .
COPY --chown=mesa:mesa documentos/ ./documentos/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MesaAyuda.Api.dll"]
