# STAGE 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Copy ONLY the project file to a specific folder to restore dependencies
COPY src/SamaBot.Api/SamaBot.Api.csproj SamaBot.Api/
WORKDIR /src/SamaBot.Api
RUN dotnet restore

# 2. Copy the rest of the source code for the specific project
COPY src/SamaBot.Api/ .

# 3. Clean up any accidental Windows binaries that might have sneaked past the .dockerignore
RUN rm -rf bin/ obj/

# 4. CRITTER STACK: Pre-generate Marten and Wolverine code
# We explicitly specify the project to avoid ambiguity
RUN dotnet run --project SamaBot.Api.csproj -- codegen write

# 5. Publish the application
RUN dotnet publish SamaBot.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# STAGE 2: Runtime (Ubuntu Chiseled - Ultra lightweight and secure)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app

# Copy only the compiled binaries from the build stage
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SamaBot.Api.dll"]