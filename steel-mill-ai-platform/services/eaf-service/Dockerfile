FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["FurnaceService.csproj", "./"]
COPY ["../../shared/models/SteelMillShared.csproj", "../../shared/models/"]
RUN dotnet restore "FurnaceService.csproj"

# Copy source code
COPY . .
COPY ../../shared/models/ ../../shared/models/

# Build
RUN dotnet build "FurnaceService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FurnaceService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FurnaceService.dll"]