FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Room match Lab/Room match Lab.csproj", "Room match Lab/"]
RUN dotnet restore "Room match Lab/Room match Lab.csproj"
COPY . .
RUN dotnet publish "Room match Lab/Room match Lab.csproj" -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Room match Lab.dll"]
