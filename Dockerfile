FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY aspnetapp/aspnetapp.csproj aspnetapp/
RUN dotnet restore aspnetapp/aspnetapp.csproj
COPY aspnetapp/ aspnetapp/
RUN dotnet publish aspnetapp/aspnetapp.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "aspnetapp.dll"]
