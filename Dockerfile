FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY testdns.csproj .
RUN dotnet restore

# copy everything else and build app
COPY . .
WORKDIR "/src/."
RUN dotnet publish -c release -o /app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
EXPOSE 53

WORKDIR /app
COPY --from=build /app ./

ENTRYPOINT ["dotnet", "testdns.dll"]
