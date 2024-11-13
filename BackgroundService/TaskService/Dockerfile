#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER app
WORKDIR /app

#设置时区为中国上海
ENV TZ=Asia/Shanghai

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src/BackgroundService/TaskService"
RUN dotnet restore "TaskService.csproj"
RUN dotnet build "TaskService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TaskService.csproj" -c Release -r linux-x64 -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskService.dll"]