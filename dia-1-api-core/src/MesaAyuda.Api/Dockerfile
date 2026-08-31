# Etapa de compilacion
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /origen

# Se copian primero los archivos de proyecto para aprovechar la cache de capas:
# mientras las dependencias no cambien, no se vuelve a restaurar.
COPY src/MesaAyuda.Api/MesaAyuda.Api.csproj src/MesaAyuda.Api/
RUN dotnet restore src/MesaAyuda.Api/MesaAyuda.Api.csproj

COPY src/ src/
RUN dotnet publish src/MesaAyuda.Api/MesaAyuda.Api.csproj -c Release -o /publicacion --no-restore

# Etapa de ejecucion
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS ejecucion
WORKDIR /aplicacion

# El proceso no corre como root.
RUN adduser --disabled-password --gecos "" --uid 5000 mesa
USER mesa

COPY --from=compilacion --chown=mesa:mesa /publicacion .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MesaAyuda.Api.dll"]
