FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AgilineeringApi/AgilineeringApi.csproj AgilineeringApi/
RUN dotnet restore AgilineeringApi/AgilineeringApi.csproj
COPY AgilineeringApi/ AgilineeringApi/
RUN dotnet publish AgilineeringApi/AgilineeringApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgilineeringApi.dll"]
