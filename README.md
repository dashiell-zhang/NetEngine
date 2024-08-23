# 背景介绍
这是一个采用最新版 .NET 框架为基础搭建的一个项目框架，之所以要做这样一个项目主要是为了在开发一个新项目时能够快速的进入业务逻辑的开发，而不需要每次去重新构建一些项目基础的内容。该项目始于2019年，从 .NET Core 2.2 时代一路迭代过来。

框架的整体技术都是以微软官方的指导进行搭建，个人喜好大道至简的风格，所以并没有对框架进行太多复杂的自定义封装，本项目主要的代码风格接近于微软官方的教程编码风格。

> 如果你在找一个简单易用的 .NET 基础项目框架，欢迎使用本项目作为基础进行尝试开发，项目包含了很多常用的模块，大家可以根据自己的需要进行裁剪只保留自己用得到的模块，毕竟越简单的东西越稳定。

# 项目介绍
项目主要整合了如下一些常用的技术点
- API 的授权认证采用 JWT 认证方式（JWT自动续期逻辑实现，到期前15分钟会签发新的Token）
- 全局异常记录实现
- Redis分布式缓存实现
- Redis分布式锁实现，支持并发所，信号量锁
- 微信和支付宝支付模块实现
- 微信小程序开发中常用的接口实现，如获取手机号，获取openid 等等
- 微信APP登录获取 token 实现
- 手机号和短信验证获取 token 实现
- 文件存储服务对接了 阿里云OSS和腾讯COS
- 短信服务对接了腾讯云和阿里云短信服务
- 雪花ID的自定义实现，支持 139年 的有效期
- 日志服务扩展，实现了数据库和本地文件两种记录模式
- 采用 Blazor 实现了一个基础的CMS管理后台，拥有完整的角色和用户权限控制
- **还包含了很多常用的Helper类和扩展方法，就不全部列举了，各位小伙伴可以克隆到本地自己研究**


# 项目大体结构
## 基础通用类库
1. Common 整合了项目开发中常用的各种静态Helper类和一些扩展方法
2. DistributedLock 目前基于 Redis 实现了一套分布式锁的方法
3. FileStorage 文件存储整合了国内常用的阿里云OSS和腾讯云COS 两家的对象存储服务
4. Logger 实现了 数据库日志记录和本地文件日志两种逻辑
5. SMS 短信模块目前整合实现 阿里云和腾讯云两家的短信服务

## 数据库层
1. Repository 用于存放数据库模型，整体采用 EF Core 最新版
2. Repository.Tool 用于操作数据库的 Add-Migration 和 Update-Database

## 定时任务项目
TaskService 支持 Cron 表达式配置周期性执行方法，未依赖任何第三方组件，原生实现的 Cron 解析方法和服务注册逻辑实现。

## Admin管理后台项目
管理后台模块 前端使用了 Blazor 技术开发，采用的是 wasm 模式，该模式可以直接将项目编译为 dll 文件运行在客户端的浏览器中，性能相对来说要高一点，并且对于服务器的压力要小很多。
1. AdminAPP 采用 Blazor 搭建的管理后台
2. AdminAPI 为AdminAPP 提供后端服务
3. AdminShared 是AdminAPP和AdminAPI公用模型存放类库，主要存放前后端交互的DTO模型

## WebAPI项目
该项目是一个 webapi 的基础项目，主要整合了如下内容：
1. 身份认证模块，
2. 支付宝支付模块，包含 PC支付  H5支付
3. 微信支付模块，包含 PC支付 H5支付 APP支付 小程序支付
4. 文件管理模块
5. 二维码生成api，图像验证码生成API
6. 微信 短信 用户名登录API
7. 缓存过滤器，并发队列限制过滤器，自定义签名算法过滤器

# 笔记
## Windows 平台服务部署方式说明
    安装
    sc.exe create MyAPI binpath= 'c:\Publish\WebAPI.exe --cd="true"' start= auto

    启动
    net start 服务名称
    如：net start MyAPI

    停止
    net stop 服务名称
    net stop MyAPI

    卸载
    sc.exe delete 服务名称
    如：sc.exe delete MyAPI

## Linux 平台服务部署方式说明

    设置应用目录权限为777
    sudo chmod 777 -R /var/appdata/MyAPI

    创建服务定义文件
    sudo vim /etc/systemd/system/MyAPI.service
    
    [Unit]
    Description=MyAPI

    [Service]
    WorkingDirectory=/var/appdata/MyAPI
    ExecStart=/var/appdata/MyAPI/MyAPI
    Restart=always
    RestartSec=60
    KillSignal=SIGINT
    SyslogIdentifier=MyAPI
    User=root

    [Install]
    WantedBy=multi-user.target
    
    保存服务定义文件并启用本服务
    sudo systemctl enable MyAPI.service

    启用该服务，并确认它正在运行。

    sudo systemctl start MyAPI.service
    sudo systemctl status MyAPI.service
    
## 数据库层说明
    All
    Microsoft.EntityFrameworkCore.Tool
    Microsoft.EntityFrameworkCore.Relational

    延迟加载
    Microsoft.EntityFrameworkCore.Proxies
    options.UseLazyLoadingProxies();

    SQLServer
    驱动：Microsoft.EntityFrameworkCore.SqlServer
    数据库生成模型指令：Scaffold-DbContext "ConnectionString" Microsoft.EntityFrameworkCore.SqlServer -OutputDir WebCore -Force
    字符串：Data Source=127.0.0.1;Initial Catalog=webcore;User ID=sa;Password=123456;Max Pool Size=100;Encrypt=True;TrustServerCertificate=True
    EF 配置：optionsBuilder.UseSqlServer("ConnectionString", o => o.MigrationsHistoryTable("__efmigrationshistory"));

    PostgreSQL
    驱动：Npgsql.EntityFrameworkCore.PostgreSQL
    数据库生成模型指令：Scaffold-DbContext "ConnectionString" Npgsql.EntityFrameworkCore.PostgreSQL -OutputDir webcore -Force
    字符串：Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456;Maximum Pool Size=30;SSL Mode=VerifyFull
    EF 配置：optionsBuilder.UseNpgsql("ConnectionString", o => o.MigrationsHistoryTable("__efmigrationshistory"));


    MySQL
    Pomelo.EntityFrameworkCore.MySql
    数据库生成模型指令：Scaffold-DbContext "ConnectionString" Pomelo.EntityFrameworkCore.MySql -OutputDir webcore -Force
    字符串：server=127.0.0.1;database=webcore;user id=root;password=123456;maxpoolsize=100
    EF 配置：optionsBuilder.UseMySql("ConnectionString", new MySqlServerVersion(new Version(8, 0, 29)), o => o.MigrationsHistoryTable("__efmigrationshistory"));

## JWT公钥和私钥生成方法
    var keyInfo = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var privateKey = Convert.ToBase64String(keyInfo.ExportECPrivateKey());
    var publicKey = Convert.ToBase64String(keyInfo.ExportSubjectPublicKeyInfo());

## 行政区划数据
数据存放在 **InitData** 文件夹的 Excel 中，excel中的数据是清洗好的，需要使用的自行导入
行政区划数据来源于百度平台，地址及清洗代码如下

目前数据截至：202104

https://lbsyun.baidu.com/index.php?title=open/dev-res


    insert into RegionArea SELECT CAST(CODE_PROV as int) AS ID, NAME_PROV AS province, '2021-04-01' AS createtime, '0' AS isdelete FROM ditu GROUP BY CODE_PROV, NAME_PROV;

    insert into RegionCity SELECT CAST(CODE_CITY as int) as id,NAME_CITY as city,CAST(CODE_PROV as int) as provinceid, '2021-04-01' AS createtime, '0' AS isdelete FROM ditu GROUP BY CODE_CITY,NAME_CITY,CODE_PROV;

    insert into RegionProvince SELECT CAST(CODE_COUN as int) as id,NAME_COUN as area,CAST(CODE_CITY as int) as cityid, '2021-04-01' AS createtime, '0' AS isdelete FROM ditu GROUP BY CODE_COUN,NAME_COUN,CODE_CITY;

    insert into RegionTown SELECT CAST(CODE_TOWN as int) as id,NAME_TOWN as town,CAST(CODE_COUN as int) as areaid, '2021-04-01' AS createtime, '0' AS isdelete FROM ditu GROUP BY CODE_TOWN,NAME_TOWN,CODE_COUN;