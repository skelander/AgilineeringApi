FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ForwardAgilityApi/ForwardAgilityApi.csproj ForwardAgilityApi/
RUN dotnet restore ForwardAgilityApi/ForwardAgilityApi.csproj
COPY ForwardAgilityApi/ ForwardAgilityApi/
RUN dotnet publish ForwardAgilityApi/ForwardAgilityApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ForwardAgilityApi.dll"]
