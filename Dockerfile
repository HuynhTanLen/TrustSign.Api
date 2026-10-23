FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Chép file project trước để lớp restore được cache khi chỉ đổi mã nguồn.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Common/TrustSign.Common.csproj src/Common/
COPY src/Domain/TrustSign.Domain.csproj src/Domain/
COPY src/Application/TrustSign.Application.csproj src/Application/
COPY src/Api/TrustSign.Api.csproj src/Api/
RUN dotnet restore src/Api/TrustSign.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/TrustSign.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "TrustSign.Api.dll"]
