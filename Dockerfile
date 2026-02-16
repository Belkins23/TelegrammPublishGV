# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TelegrammPublishGV.csproj", "."]
RUN dotnet restore "./TelegrammPublishGV.csproj"
COPY . .
RUN dotnet publish "./TelegrammPublishGV.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Проброс на все интерфейсы
ENV ASPNETCORE_URLS=http://+:5689
EXPOSE 5689

ENTRYPOINT ["dotnet", "TelegrammPublishGV.dll"]
