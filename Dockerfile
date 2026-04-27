FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Backend/CentralAuthNotificationPlatform.csproj Backend/
RUN dotnet restore Backend/CentralAuthNotificationPlatform.csproj

COPY Backend/ Backend/
RUN dotnet publish Backend/CentralAuthNotificationPlatform.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralAuthNotificationPlatform.dll"]
