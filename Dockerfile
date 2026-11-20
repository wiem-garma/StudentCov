# ---------- Étape 1 : compilation ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restauration des packages (mise en cache si le .csproj ne change pas)
COPY CovStudent.csproj .
RUN dotnet restore CovStudent.csproj

# Compilation et publication
COPY . .
RUN dotnet publish CovStudent.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Étape 2 : image d'exécution (légère) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Sécurité : on n'exécute pas l'application en root
USER app

ENTRYPOINT ["dotnet", "CovStudent.dll"]