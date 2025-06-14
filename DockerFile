# Stage 1 - Build
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /app

# Copy csproj and restore
COPY Summlyzer/Summlyzer.csproj ./Summlyzer/
RUN dotnet restore ./Summlyzer/Summlyzer.csproj

# Copy the rest of the source code
COPY Summlyzer/ ./Summlyzer/
WORKDIR /app/Summlyzer

# Publish the app
RUN dotnet publish -c Release -o /app/out

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "Summlyzer.dll"]
