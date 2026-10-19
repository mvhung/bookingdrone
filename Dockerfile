# ==========================================
# Giai đoạn 1: Build Frontend (Vue 3 + Vite)
# ==========================================
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm install

COPY frontend/ ./
RUN npm run build

# ==========================================
# Giai đoạn 2: Build Backend (.NET 8 Web API)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY backend/EventMap.Api.csproj ./backend/
RUN dotnet restore ./backend/EventMap.Api.csproj

COPY backend/ ./backend/
# Nhúng thẳng giao diện đã build vào thư mục wwwroot của .NET
COPY --from=frontend-build /app/frontend/dist ./backend/wwwroot

WORKDIR /src/backend
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Giai đoạn 3: Runtime (.NET 8 Runtime)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Tạo thư mục lưu trữ cơ sở dữ liệu SQLite
RUN mkdir -p /app/data

COPY --from=backend-build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "EventMap.Api.dll"]
