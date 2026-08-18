# 部署配置生成器

`Deployment.Generator` 用于根据一份统一配置生成以下部署文件：

- Nginx 主配置参考文件
- Admin.App、Admin.WebAPI、Client.WebAPI 的 Nginx 配置
- Admin.WebAPI、Client.WebAPI、TaskService 的 systemd 服务配置
- systemd 服务启用与启动命令
- Admin.App、Admin.WebAPI、Client.WebAPI、TaskService 的云效流水线 YAML

生成器只负责生成文件，不会连接服务器、复制文件、安装服务或执行部署命令

## 快速使用

1. 修改 `Deployment/Deployment.Generator/deploysettings.json`
2. 在解决方案根目录执行：

   ```bash
   dotnet run --project Deployment/Deployment.Generator
   ```

3. 在 `Deployment/Deployment.Generator/Generated` 中查看生成结果

生成器不接受启动参数，固定读取 `deploysettings.json` 和 `Templates`。生成过程会先在临时目录中完成，只有全部文件生成成功后才会完整替换现有 `Generated`，配置或模板错误不会留下不完整的生成结果

## 运行环境

### 生成器运行环境

- .NET 10 SDK
- 在 Windows、macOS 或 Linux 上均可运行

### 目标服务器环境

- Linux x64
- Nginx
- systemd
- `tar`

当前生成的 Nginx、systemd 和云效流水线配置面向 Linux 服务器，其中 Admin.WebAPI、Client.WebAPI 和 TaskService 使用 `linux-x64` 自包含发布

## 目录结构

```text
Deployment/
└─ Deployment.Generator/
   ├─ Generated/
   │  ├─ nginx/
   │  ├─ service/
   │  └─ yunxiao/
   ├─ Templates/
   │  ├─ nginx/
   │  ├─ service/
   │  └─ yunxiao/
   ├─ Deployment.Generator.csproj
   ├─ DeploymentSettings.cs
   ├─ Program.cs
   └─ deploysettings.json
```

- `deploysettings.json`：需要维护的部署参数
- `Templates`：生成文件使用的模板
- `Generated`：最终生成结果

不要直接修改 `Generated` 中的文件。生成器每次运行都会使用当前配置和模板完整替换该目录，其中自行添加的额外文件也会被删除

## 配置说明

配置文件位于：

```text
Deployment/Deployment.Generator/deploysettings.json
```

仓库中提交的是 Demo 数据，使用前应先将其修改为当前项目的部署信息

### ProjectName

```json
"ProjectName": "demo"
```

项目名称用于生成文件名、systemd 服务名、日志标识和云效制品文件名

允许使用：

- 英文字母
- 数字
- 中间连字符

项目名称必须以字母或数字开头和结尾，生成文件名时会自动转换为小写

### Host

```json
"Host": {
  "AdminApp": {
    "Domain": "admin.example.com",
    "CertificateFile": "ssl/admin.example.com.pem",
    "CertificateKeyFile": "ssl/admin.example.com.key"
  },
  "AdminWebAPI": {
    "Domain": "admin-webapi.example.com",
    "CertificateFile": "ssl/admin-webapi.example.com.pem",
    "CertificateKeyFile": "ssl/admin-webapi.example.com.key"
  },
  "ClientWebAPI": {
    "Domain": "client-webapi.example.com",
    "CertificateFile": "ssl/client-webapi.example.com.pem",
    "CertificateKeyFile": "ssl/client-webapi.example.com.key"
  }
}
```

每个宿主分别配置域名、证书和证书私钥

如果三个域名使用同一张通配证书，可以在三个节点中填写相同的证书路径。如果使用独立证书，则分别填写对应路径

`Domain` 只填写域名，不包含 `http://`、`https://`、请求路径或末尾 `/`

证书路径会直接写入 Nginx 配置，应与服务器上的实际位置保持一致

### Port

```json
"Port": {
  "AdminWebAPI": 30011,
  "ClientWebAPI": 30012
}
```

两个端口必须满足以下要求：

- 位于 `1` 到 `65535` 之间
- 不能使用相同端口

服务只监听服务器回环地址 `127.0.0.1`，由 Nginx 对外提供访问入口

### DeployRoot

```json
"DeployRoot": "/var/appdata/demo"
```

服务器上的项目部署根目录。四个项目使用以下固定子目录：

```text
/var/appdata/demo/adminapp
/var/appdata/demo/adminwebapi
/var/appdata/demo/clientwebapi
/var/appdata/demo/taskservice
```

部署根目录必须满足以下要求：

- 必须是非根目录的 Linux 绝对路径
- 路径段只能包含英文字母、数字、点、下划线和中间连字符
- 不能包含 `.` 或 `..` 路径段
- 可以在末尾保留一个 `/`，生成文件时会自动移除

### YunXiao

```json
"YunXiao": {
  "Codeup": {
    "Name": "DemoProject",
    "Endpoint": "https://codeup.aliyun.com/example/demo-project.git",
    "Branch": "main",
    "ServiceConnection": "demo-service-connection"
  },
  "MachineGroup": "demo-machine-group"
}
```

| 配置项 | 说明 |
|---|---|
| `Codeup.Name` | 云效流水线中的代码源名称 |
| `Codeup.Endpoint` | Codeup 仓库地址 |
| `Codeup.Branch` | 流水线构建分支 |
| `Codeup.ServiceConnection` | 云效代码源服务连接标识 |
| `MachineGroup` | 云效主机部署任务使用的机器组标识 |

## 生成结果

假设 `ProjectName` 为 `demo`，生成结果如下：

```text
Generated/
├─ nginx/
│  ├─ nginx.conf
│  ├─ demo-admin-app.conf
│  ├─ demo-admin-webapi.conf
│  └─ demo-client-webapi.conf
├─ service/
│  ├─ demo-admin-webapi.service
│  ├─ demo-client-webapi.service
│  ├─ demo-task-service.service
│  └─ demo-install-services.txt
└─ yunxiao/
   ├─ demo-admin-app.yaml
   ├─ demo-admin-webapi.yaml
   ├─ demo-client-webapi.yaml
   └─ demo-task-service.yaml
```

## 首次部署准备

首次部署前，需要在目标服务器完成以下准备：

- 安装并启动 Nginx
- 创建 `DeployRoot` 配置对应的部署根目录
- 在部署根目录中创建 `adminapp`、`adminwebapi`、`clientwebapi` 和 `taskservice` 子目录
- 将 SSL 证书放到 Nginx 配置引用的位置
- 将生成的 systemd 服务文件复制到 `/etc/systemd/system`
- 执行 `systemctl daemon-reload` 使 systemd 加载服务配置
- 确认云效部署任务使用的账号可以操作部署目录和对应的 systemd 服务

`Generated/service/*-install-services.txt` 中的命令会直接启用并启动服务，因此执行前应确保对应发布文件已经部署到目标目录。如果通过云效完成首次发布，应先准备部署目录和 systemd 服务配置，再运行流水线解压制品并启动服务

## Nginx 配置

`Generated/nginx/nginx.conf` 是 Nginx 主配置参考文件，其中包含 API 配置依赖的 `$connection_upgrade` 定义

服务器已经存在 `/etc/nginx/nginx.conf` 时，应先对照并合并配置，不要未经确认直接覆盖

三个项目配置通常放入：

```text
/etc/nginx/conf.d/
```

同时需要将证书放到配置指定的位置。完成后可在服务器上检查并重载 Nginx：

```bash
sudo nginx -t
sudo systemctl reload nginx
```

## systemd 服务

先将三份 `.service` 文件复制到：

```text
/etc/systemd/system/
```

然后参考 `Generated/service/demo-install-services.txt` 执行服务重载、开机启用和启动命令

该 TXT 文件只提供命令，不会自动复制或安装 `.service` 文件

systemd 配置要求三个项目使用 Linux 自包含发布，并且发布目录中存在以下可执行文件：

```text
adminwebapi/Admin.WebAPI
clientwebapi/Client.WebAPI
taskservice/TaskService
```

## 云效流水线

`Generated/yunxiao` 下的四份 YAML 分别对应四个 Presentation 项目，可用于创建对应的云效流水线

流水线使用以下 SDK 镜像：

```text
mcr.microsoft.com/dotnet/sdk:10.0
```

Admin.App 流水线会在构建工作区中完成以下操作：

- 安装发布所需环境与 `wasm-tools`
- 将 `Presentation/Admin.App/Program.cs` 中的本地 API 地址替换为 `Host.AdminWebAPI.Domain`
- 发布 Blazor WebAssembly 静态文件

地址替换依赖 `Program.cs` 中存在完整的 `https://localhost:9833/` 字符串。如果以后修改了本地 API 地址或配置方式，需要同步调整 `Templates/yunxiao/Admin.App.yaml.template` 中的替换命令

首次安装 workload 可能耗时较长。如果 Admin.App 流水线超过默认的 5 分钟限制，可适当增加模板中的 `timeoutMinutes`

部署命令使用 `rm <项目目录>/*` 清理一级目录中的非隐藏文件和符号链接，不会匹配隐藏文件，已有子目录也会保留。这可以保留通过项目上传接口写入子目录的文件，但隐藏文件以及新版本中已经移除的旧子目录文件也会继续保留

清理完成后，部署命令会通过 `chmod 777 -R <项目目录>/*` 递归修改非隐藏文件和子目录的权限

## 修改模板

如需调整最终配置内容，应修改 `Templates` 中对应模板，然后重新运行生成器

模板占位符使用以下格式：

```text
{{Host.AdminApp.Domain}}
{{Port.AdminWebAPI}}
{{ProjectNameLower}}
```

生成器发现未替换占位符时会停止生成，并保留原有 `Generated` 结果
