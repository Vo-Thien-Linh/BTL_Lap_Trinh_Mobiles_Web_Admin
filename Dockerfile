FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Web_Admin_Booking_App/Web_Admin_Booking_App.csproj Web_Admin_Booking_App/
RUN dotnet restore Web_Admin_Booking_App/Web_Admin_Booking_App.csproj

COPY . .
RUN dotnet publish Web_Admin_Booking_App/Web_Admin_Booking_App.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet Web_Admin_Booking_App.dll --urls http://0.0.0.0:${PORT:-8080}"]