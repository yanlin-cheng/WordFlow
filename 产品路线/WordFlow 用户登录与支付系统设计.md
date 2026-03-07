# WordFlow 用户登录与支付系统设计

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿  
> MVP 版本：极简设计

---

## 目录

1. [系统架构](#1-系统架构)
2. [国内版设计](#2-国内版设计)
3. [国际版设计](#3-国际版设计)
4. [云端 API 设计](#4-云端 api-设计)
5. [数据库设计](#5-数据库设计)
6. [离线授权验证](#6-离线授权验证)
7. [安全机制](#7-安全机制)
8. [实施计划](#8-实施计划)

---

## 1. 系统架构

### 1.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                        WordFlow 登录支付系统                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                      客户端 (Windows)                        │   │
│  ├─────────────────────────────────────────────────────────────┤   │
│  │                                                             │   │
│  │  ┌─────────────────┐              ┌─────────────────┐      │   │
│  │  │   国内版        │              │   国际版        │      │   │
│  │  │   微信扫码登录   │              │   邮箱 + 验证码    │      │   │
│  │  │   微信支付       │              │   PayPal 支付    │      │   │
│  │  └─────────────────┘              └─────────────────┘      │   │
│  │                                                             │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              ↓                                      │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                      云端服务 (轻量级)                       │   │
│  ├─────────────────────────────────────────────────────────────┤   │
│  │                                                             │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │   │
│  │  │ 用户服务     │  │ 支付服务     │  │ 词典包授权服务       │ │   │
│  │  │ User API    │  │ Payment API │  │ License API         │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘ │   │
│  │                                                             │   │
│  │  ┌─────────────────────────────────────────────────────┐   │   │
│  │  │              MongoDB (用户 + 购买记录)               │   │   │
│  │  └─────────────────────────────────────────────────────┘   │   │
│  │                                                             │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              ↓                                      │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                      第三方支付                              │   │
│  ├─────────────────────────────────────────────────────────────┤   │
│  │                                                             │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │   │
│  │  │  微信开放平台 │  │  虎皮椒支付  │  │  PayPal API        │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘ │   │
│  │                                                             │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 核心设计原则

| 原则 | 说明 |
|------|------|
| **本地优先** | 核心功能本地运行，云端只存储授权信息 |
| **极简 MVP** | 最小化云端负载，快速上线验证 |
| **离线可用** | 已购词典包离线状态下仍可使用 |
| **轻量验证** | 定期联网验证，非每次使用都验证 |

---

## 2. 国内版设计

### 2.1 微信扫码登录

#### 登录流程

```
┌─────────────────────────────────────────────────────────────┐
│                    微信扫码登录流程                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 用户点击"微信登录"                                       │
│     ↓                                                       │
│  2. 客户端请求云端获取二维码                                 │
│     ↓                                                       │
│  3. 显示微信登录二维码                                       │
│     ↓                                                       │
│  4. 用户微信扫码确认                                         │
│     ↓                                                       │
│  5. 云端轮询检查登录状态                                     │
│     ↓                                                       │
│  6. 登录成功，返回用户信息 + Token                           │
│     ↓                                                       │
│  7. 本地保存 Token，完成登录                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### API 接口

```csharp
// 1. 获取登录二维码
GET /api/v1/auth/wechat/qr-code
Response: {
  "qrCodeUrl": "https://...",
  "scanId": "scan_xxx123",
  "expireAt": "2026-03-07T12:10:00Z"
}

// 2. 轮询检查登录状态
GET /api/v1/auth/wechat/check?scanId=scan_xxx123
Response: {
  "status": "pending",  // pending / success / expired
  "token": "jwt_xxx",   // 登录成功时返回
  "user": { ... }       // 用户信息
}

// 3. 刷新 Token
POST /api/v1/auth/refresh
Body: { "refreshToken": "xxx" }
```

#### 微信开放平台配置

1. 注册微信开放平台账号（https://open.weixin.qq.com/）
2. 创建网站应用，获取 AppID 和 AppSecret
3. 配置回调域名
4. 接入微信支付需额外申请

---

### 2.2 微信支付

#### 支付流程

```
┌─────────────────────────────────────────────────────────────┐
│                      微信支付流程                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 用户选择购买词典包                                       │
│     ↓                                                       │
│  2. 客户端请求云端创建支付订单                               │
│     ↓                                                       │
│  3. 云端调用微信支付 API 创建预支付订单                        │
│     ↓                                                       │
│  4. 返回支付二维码 URL                                        │
│     ↓                                                       │
│  5. 显示支付二维码                                           │
│     ↓                                                       │
│  6. 用户微信扫码支付                                         │
│     ↓                                                       │
│  7. 微信支付回调云端通知支付结果                             │
│     ↓                                                       │
│  8. 云端更新购买记录，返回成功                               │
│     ↓                                                       │
│  9. 客户端提示支付成功，解锁词典包                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### API 接口

```csharp
// 1. 创建支付订单
POST /api/v1/payment/create-order
Body: {
  "packId": "pack_001",
  "currency": "CNY",
  "paymentMethod": "wechat"
}
Response: {
  "orderId": "order_xxx",
  "qrCodeUrl": "https://...",
  "amount": 29.00,
  "expireAt": "2026-03-07T12:15:00Z"
}

// 2. 查询支付状态
GET /api/v1/payment/order/{orderId}/status
Response: {
  "status": "pending",  // pending / success / failed
  "paidAt": "2026-03-07T12:10:00Z"
}
```

#### 接入方案

**推荐：虎皮椒支付**（https://www.xunhupay.com/）
- 个人可接入
- 费率：约 2%
- 支持微信、支付宝
- 提供完整 API 文档

---

## 3. 国际版设计

### 3.1 邮箱验证码登录

#### 登录流程

```
┌─────────────────────────────────────────────────────────────┐
│                    邮箱验证码登录流程                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 用户输入邮箱地址                                         │
│     ↓                                                       │
│  2. 客户端请求发送验证码                                     │
│     ↓                                                       │
│  3. 云端发送验证码邮件                                        │
│     ↓                                                       │
│  4. 用户输入收到的验证码                                     │
│     ↓                                                       │
│  5. 客户端验证验证码                                         │
│     ↓                                                       │
│  6. 验证成功，返回用户信息 + Token                           │
│     ↓                                                       │
│  7. 本地保存 Token，完成登录                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### API 接口

```csharp
// 1. 发送验证码
POST /api/v1/auth/email/send-code
Body: { "email": "user@example.com" }
Response: {
  "codeId": "code_xxx",
  "expireAt": "2026-03-07T12:15:00Z",  // 15 分钟有效
  "retryAfter": 60  // 60 秒后可重发
}

// 2. 验证验证码并登录
POST /api/v1/auth/email/login
Body: {
  "email": "user@example.com",
  "code": "123456",
  "codeId": "code_xxx"
}
Response: {
  "token": "jwt_xxx",
  "refreshToken": "refresh_xxx",
  "user": {
    "userId": "usr_xxx",
    "email": "user@example.com",
    "isNewUser": true
  }
}
```

#### 邮件服务推荐

| 服务 | 免费额度 | 说明 |
|------|---------|------|
| **SendGrid** | 100 封/天 | Twilio 旗下，稳定可靠 |
| **Mailgun** | 5000 封/月 | 开发者友好 |
| **阿里云邮件推送** | 200 封/天 | 国内访问快 |

---

### 3.2 PayPal 支付

#### 支付流程

```
┌─────────────────────────────────────────────────────────────┐
│                      PayPal 支付流程                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 用户选择购买词典包                                       │
│     ↓                                                       │
│  2. 客户端请求云端创建支付订单                               │
│     ↓                                                       │
│  3. 云端调用 PayPal API 创建订单                              │
│     ↓                                                       │
│  4. 返回 PayPal 支付链接                                       │
│     ↓                                                       │
│  5. 打开 PayPal 支付页面（浏览器或内嵌）                      │
│     ↓                                                       │
│  6. 用户登录 PayPal 并确认支付                                │
│     ↓                                                       │
│  7. PayPal 回调云端通知支付结果                               │
│     ↓                                                       │
│  8. 云端更新购买记录，返回成功                               │
│     ↓                                                       │
│  9. 客户端提示支付成功，解锁词典包                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### API 接口

```csharp
// 1. 创建 PayPal 订单
POST /api/v1/payment/paypal/create-order
Body: {
  "packId": "pack_001",
  "currency": "USD",
  "amount": 4.99
}
Response: {
  "orderId": "paypal_order_xxx",
  "approvalUrl": "https://www.paypal.com/checkout/...",
  "expireAt": "2026-03-07T12:15:00Z"
}

// 2. 捕获支付（用户完成 PayPal 支付后）
POST /api/v1/payment/paypal/capture
Body: {
  "orderId": "paypal_order_xxx",
  "payerId": "payer_xxx"
}
Response: {
  "status": "completed",
  "transactionId": "txn_xxx"
}
```

#### PayPal 接入

**个人账户即可接入**：
1. 注册 PayPal 账号（https://www.paypal.com/）
2. 申请 PayPal Checkout
3. 获取 Client ID 和 Secret
4. 集成 PayPal SDK

**费率**：约 3.9% + 固定费用（因国家而异）

---

## 4. 云端 API 设计

### 4.1 API 概览

| 接口 | 方法 | 说明 |
|------|------|------|
| `/api/v1/auth/wechat/qr-code` | GET | 获取微信登录二维码 |
| `/api/v1/auth/wechat/check` | GET | 检查微信登录状态 |
| `/api/v1/auth/email/send-code` | POST | 发送邮箱验证码 |
| `/api/v1/auth/email/login` | POST | 邮箱验证码登录 |
| `/api/v1/auth/refresh` | POST | 刷新 Token |
| `/api/v1/auth/logout` | POST | 登出 |
| `/api/v1/payment/create-order` | POST | 创建支付订单 |
| `/api/v1/payment/order/{id}/status` | GET | 查询订单状态 |
| `/api/v1/user/purchases` | GET | 获取已购词典包 |
| `/api/v1/user/license/verify` | POST | 验证词典包授权 |

### 4.2 统一响应格式

```csharp
public class ApiResponse<T>
{
    public int Code { get; set; }      // 0=成功，其他=错误码
    public string Message { get; set; }
    public T Data { get; set; }
}

// 成功响应示例
{
    "code": 0,
    "message": "success",
    "data": { ... }
}

// 错误响应示例
{
    "code": 1001,
    "message": "验证码已过期",
    "data": null
}
```

### 4.3 错误码定义

```csharp
public static class ErrorCodes
{
    // 认证相关 (1000-1999)
    public const int AuthCodeExpired = 1001;      // 验证码过期
    public const int AuthCodeInvalid = 1002;      // 验证码无效
    public const int AuthTokenExpired = 1003;     // Token 过期
    public const int AuthTokenInvalid = 1004;     // Token 无效
    public const int AuthQrCodeExpired = 1005;    // 二维码过期
    
    // 支付相关 (2000-2999)
    public const int PaymentOrderNotFound = 2001; // 订单不存在
    public const int PaymentOrderExpired = 2002;  // 订单已过期
    public const int PaymentAlreadyPaid = 2003;   // 订单已支付
    public const int PaymentFailed = 2004;        // 支付失败
    
    // 授权相关 (3000-3999)
    public const int LicenseNotFound = 3001;      // 授权不存在
    public const int LicenseInvalid = 3002;       // 授权无效
}
```

---

## 5. 数据库设计

### 5.1 MongoDB 集合设计

#### 用户集合 (users)

```javascript
{
  "_id": ObjectId("..."),
  "userId": "usr_abc123",           // 业务 ID（唯一）
  "email": "user@example.com",      // 邮箱（国际版）
  "wechatOpenId": "wx_xxx",         // 微信 OpenID（国内版）
  "wechatUnionId": "wx_union_xxx",  // 微信 UnionID（可选）
  "createdAt": ISODate("2026-03-07T00:00:00Z"),
  "lastLoginAt": ISODate("2026-03-07T12:00:00Z"),
  "devices": [                      // 登录设备列表
    {
      "deviceId": "dev_xxx",
      "deviceName": "Windows-PC",
      "lastLoginAt": ISODate("2026-03-07T12:00:00Z")
    }
  ]
}
```

#### 购买记录集合 (purchases)

```javascript
{
  "_id": ObjectId("..."),
  "purchaseId": "pur_xyz789",       // 业务 ID（唯一）
  "userId": "usr_abc123",           // 用户 ID
  "packId": "pack_001",             // 词典包 ID
  "amount": 29.00,
  "currency": "CNY",                // CNY / USD
  "paymentMethod": "wechat",        // wechat / paypal
  "paymentTransactionId": "wx_xxx", // 第三方支付交易 ID
  "status": "completed",            // pending / completed / failed
  "purchaseDate": ISODate("2026-03-07T12:00:00Z"),
  "expireAt": null                  // 永久有效为 null
}
```

#### 验证码集合 (verificationCodes)

```javascript
{
  "_id": ObjectId("..."),
  "codeId": "code_xxx",
  "email": "user@example.com",
  "code": "123456",
  "type": "login",                  // login / register
  "status": "pending",              // pending / used / expired
  "createdAt": ISODate("2026-03-07T12:00:00Z"),
  "expireAt": ISODate("2026-03-07T12:15:00Z")
}
```

### 5.2 索引设计

```javascript
// users 集合索引
db.users.createIndex({ "userId": 1 }, { unique: true })
db.users.createIndex({ "email": 1 }, { unique: true, sparse: true })
db.users.createIndex({ "wechatOpenId": 1 }, { unique: true, sparse: true })

// purchases 集合索引
db.purchases.createIndex({ "purchaseId": 1 }, { unique: true })
db.purchases.createIndex({ "userId": 1, "packId": 1 })  // 查询用户购买
db.purchases.createIndex({ "paymentTransactionId": 1 }) // 防重复

// verificationCodes 集合索引
db.verificationCodes.createIndex({ "codeId": 1 }, { unique: true })
db.verificationCodes.createIndex({ "email": 1, "expireAt": 1 })
```

---

## 6. 离线授权验证

### 6.1 授权验证流程

```
┌─────────────────────────────────────────────────────────────┐
│                    离线授权验证流程                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  首次登录（在线）：                                         │
│  1. 用户登录成功                                            │
│  2. 云端返回用户已购词典包列表 + 授权证书                    │
│  3. 本地保存授权证书（加密存储）                            │
│                                                             │
│  离线使用：                                                 │
│  1. 用户打开词典包                                          │
│  2. 本地验证授权证书                                        │
│  3. 验证通过，允许使用                                      │
│                                                             │
│  定期验证（联网时）：                                       │
│  1. 每 7 天自动联网验证一次                                   │
│  2. 云端返回最新授权状态                                    │
│  3. 更新本地授权证书                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 授权证书格式

```json
// 授权证书（JWT 格式）
{
  "userId": "usr_abc123",
  "purchasedPacks": ["pack_001", "pack_002"],
  "issuedAt": 1709784000,
  "expiresAt": 1710388800,  // 7 天后过期
  "signature": "xxx"        // 云端签名
}
```

### 6.3 本地验证代码

```csharp
public class LicenseVerifier
{
    private readonly string _publicKey;
    
    public LicenseVerifier(string publicKey)
    {
        _publicKey = publicKey;
    }
    
    /// <summary>
    /// 验证授权证书
    /// </summary>
    public bool VerifyLicense(string licenseToken)
    {
        try
        {
            // 1. 验证 JWT 签名
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new RsaSecurityKey(_publicKey),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true
            };
            
            handler.ValidateToken(licenseToken, validationParameters, out _);
            
            // 2. 检查是否过期
            var token = handler.ReadJwtToken(licenseToken);
            var expiresAt = token.Claims.First(c => c.Type == "exp").Value;
            var expiresTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiresAt));
            
            if (expiresTime < DateTimeOffset.Now)
            {
                return false;  // 已过期
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检查是否有词典包授权
    /// </summary>
    public bool HasPackAccess(string licenseToken, string packId)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(licenseToken);
        var packsClaim = token.Claims.FirstOrDefault(c => c.Type == "purchasedPacks")?.Value;
        
        if (string.IsNullOrEmpty(packsClaim))
        {
            return false;
        }
        
        var packs = JsonConvert.DeserializeObject<List<string>>(packsClaim);
        return packs.Contains(packId);
    }
}
```

---

## 7. 安全机制

### 7.1 Token 安全

```csharp
// JWT Token 配置
public class JwtConfig
{
    public string SecretKey { get; set; }      // 密钥（至少 32 位）
    public string Issuer { get; set; }         // 签发者
    public int AccessTokenExpiry { get; set; } = 60;    // Access Token 过期时间（分钟）
    public int RefreshTokenExpiry { get; set; } = 10080; // Refresh Token 过期时间（分钟）
}

// Token 生成
public string GenerateToken(UserInfo user)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    
    var claims = new[]
    {
        new Claim("userId", user.UserId),
        new Claim("email", user.Email)
    };
    
    var token = new JwtSecurityToken(
        issuer: _config.Issuer,
        claims: claims,
        expires: DateTime.Now.AddMinutes(_config.AccessTokenExpiry),
        signingCredentials: credentials
    );
    
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### 7.2 防破解机制

| 机制 | 说明 |
|------|------|
| **代码混淆** | 使用 Dotfuscator 混淆关键代码 |
| **签名验证** | 授权证书使用非对称加密签名 |
| **设备绑定** | 限制单账号最多登录 3 台设备 |
| **定期验证** | 每 7 天联网验证一次授权 |
| **异常检测** | 检测频繁重装、多设备登录等异常行为 |

### 7.3 本地加密存储

```csharp
public class SecureStorage
{
    /// <summary>
    /// 加密保存敏感数据
    /// </summary>
    public void Save(string key, string value)
    {
        // 使用 Windows DPAPI 加密
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            null,
            DataProtectionScope.CurrentUser
        );
        
        File.WriteAllBytes(GetPath(key), encrypted);
    }
    
    /// <summary>
    /// 解密读取数据
    /// </summary>
    public string Load(string key)
    {
        var encrypted = File.ReadAllBytes(GetPath(key));
        var decrypted = ProtectedData.Unprotect(
            encrypted,
            null,
            DataProtectionScope.CurrentUser
        );
        
        return Encoding.UTF8.GetString(decrypted);
    }
}
```

---

## 8. 实施计划

### 8.1 第一阶段：基础架构（1-2 周）

- [ ] 搭建 MongoDB 数据库
- [ ] 实现用户服务 API
- [ ] 实现 JWT Token 生成与验证
- [ ] 客户端基础登录 UI

### 8.2 第二阶段：国内版登录支付（2-3 周）

- [ ] 微信开放平台申请与配置
- [ ] 微信扫码登录实现
- [ ] 虎皮椒支付接入
- [ ] 微信支付实现
- [ ] 购买记录保存

### 8.3 第三阶段：国际版登录支付（2-3 周）

- [ ] 邮件服务接入（SendGrid）
- [ ] 邮箱验证码登录实现
- [ ] PayPal 账号申请与配置
- [ ] PayPal 支付实现

### 8.4 第四阶段：离线授权（1 周）

- [ ] 授权证书生成与下发
- [ ] 本地授权验证实现
- [ ] 定期联网验证机制
- [ ] 设备绑定限制

### 8.5 第五阶段：安全加固（1 周）

- [ ] 代码混淆
- [ ] 防破解机制
- [ ] 异常检测
- [ ] 安全测试

---

## 附录

### A. 第三方服务清单

| 服务 | 用途 | 链接 |
|------|------|------|
| 微信开放平台 | 国内登录 + 支付 | https://open.weixin.qq.com/ |
| 虎皮椒 | 支付接入 | https://www.xunhupay.com/ |
| SendGrid | 邮件发送 | https://sendgrid.com/ |
| PayPal | 国际支付 | https://www.paypal.com/ |
| MongoDB Atlas | 数据库 | https://www.mongodb.com/cloud/atlas |

### B. 成本估算

| 服务 | 免费额度 | 初期成本 |
|------|---------|---------|
| MongoDB Atlas | 512MB | $0 |
| SendGrid | 100 封/天 | $0 |
| 微信开放平台 | - | ¥0（认证费 300 元/年） |
| PayPal | - | $0 |
| 虎皮椒 | - | ¥0（交易费率 2%） |

**初期总成本**：约 ¥300（微信认证费）+ 支付交易费率

---

*本文档为 WordFlow 用户登录与支付系统设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
