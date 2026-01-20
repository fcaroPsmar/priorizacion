# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Prioritizacion.Web.csproj ./
RUN dotnet restore ./Prioritizacion.Web.csproj
COPY . ./
RUN dotnet publish ./Prioritizacion.Web.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Prioritizacion.Web.dll"]
