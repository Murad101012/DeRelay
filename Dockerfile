# Building and Publishing
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src 
COPY src/ .
RUN dotnet restore "DeRelay.Api/DeRelay.Api.csproj"
RUN dotnet publish "DeRelay.Api/DeRelay.Api.csproj" -c Release -p:UseAppHost=false -o /app/publish

# Preparing runtime enviroment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# To change port of DotNet app. 
# This will ignore port value in launchSettings.json/"applicationUrl": "http://localhost:5086"
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DeRelay.Api.dll"]