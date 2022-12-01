#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

#修改镜像内部软件源采用阿里镜像
RUN echo "deb https://mirrors.aliyun.com/debian bullseye main" > /etc/apt/sources.list
RUN echo "deb https://mirrors.aliyun.com/debian-security bullseye-security main" >> /etc/apt/sources.list
RUN echo "deb https://mirrors.aliyun.com/debian bullseye-updates main" >> /etc/apt/sources.list

#安装 libfontconfig1 以解决 SkiaSharp 在 linux 的依赖问题
RUN apt-get update && apt-get -y install libfontconfig1

#设置时区为中国上海
ENV TZ=Asia/Shanghai

EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

WORKDIR /src
COPY ["AdminAPI/AdminAPI.csproj", "AdminAPI/"]
RUN dotnet restore "AdminAPI/AdminAPI.csproj"
COPY . .
WORKDIR "/src/AdminAPI"
RUN dotnet build "AdminAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdminAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdminAPI.dll"]