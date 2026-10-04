# Divan: builds RMuseum (API) or DivanRazor (site) for Linux.
# The projects target net10.0-windows7.0 (their RSecurityBackend dependency only ships for it), but they
# only use the plain .NET + ASP.NET Core runtimes, so the framework-dependent output runs on Linux.
#   docker build --build-arg PROJECT=RMuseum .
#   docker build --build-arg PROJECT=DivanRazor .
# SDK pinned to RMuseum/global.json: newer Razor compilers (10.0.4xx) reject some upstream views
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish "$PROJECT/$PROJECT.csproj" -c Release -o /app -p:EnableWindowsTargeting=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG PROJECT
ENV APP_DLL=$PROJECT.dll ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
COPY deploy/entrypoint.sh /entrypoint.sh
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
