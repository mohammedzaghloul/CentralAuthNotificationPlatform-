FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["Backend/CentralAuthNotificationPlatform.PL.csproj", "Backend/"]
COPY ["CentralAuthNotificationPlatform.BLL/CentralAuthNotificationPlatform.BLL.csproj", "CentralAuthNotificationPlatform.BLL/"]
COPY ["CentralAuthNotificationPlatform.DAL/CentralAuthNotificationPlatform.DAL.csproj", "CentralAuthNotificationPlatform.DAL/"]

RUN dotnet restore "Backend/CentralAuthNotificationPlatform.PL.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Backend"
RUN dotnet publish "CentralAuthNotificationPlatform.PL.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CentralAuthNotificationPlatform.PL.dll"]
