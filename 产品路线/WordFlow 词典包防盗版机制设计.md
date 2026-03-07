# WordFlow 词典包防盗版机制设计

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [威胁分析](#1-威胁分析)
2. [加密方案](#2-加密方案)
3. [验证机制](#3-验证机制)
4. [VSCode 模式市场集成](#4-vscode 模式市场集成)
5. [水印追踪机制](#5-水印追踪机制)
6. [法律维权](#6-法律维权)

---

## 1. 威胁分析

### 1.1 潜在盗版方式

| 盗版方式 | 说明 | 风险等级 |
|---------|------|---------|
| **文件复制** | 直接复制词典包文件分享给他人 | 高 |
| **反编译提取** | 反编译客户端提取解密逻辑 | 中 |
| **账户共享** | 多人共享一个账户 | 中 |
| **破解验证** | 绕过在线验证机制 | 低 |
| **录屏泄露** | 录制词典内容公开传播 | 低 |

### 1.2 防护目标

1. **增加盗版成本**：让破解变得困难，不值得花时间
2. **追溯泄露源**：能够追踪到是谁泄露的
3. **不影响正常用户**：防盗措施不影响正版用户体验

### 1.3 防护原则

| 原则 | 说明 |
|------|------|
| **适度保护** | 不过度影响用户体验 |
| **多层防护** | 多种技术手段结合 |
| **可追溯** | 能够追踪泄露源 |
| **持续更新** | 根据新威胁更新防护 |

---

## 2. 加密方案

### 2.1 整体加密架构

```
┌─────────────────────────────────────────────────────────┐
│                    词典包加密流程                        │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  创作者上传明文词典包                                    │
│       ↓                                                 │
│  云端审核通过                                            │
│       ↓                                                 │
│  生成用户专属密钥 (per-user key)                         │
│       ↓                                                 │
│  使用密钥加密词典内容                                    │
│       ↓                                                 │
│  存储加密后的词典包 + 密钥索引                           │
│       ↓                                                 │
│  用户购买后下载                                          │
│       ↓                                                 │
│  客户端使用用户密钥解密                                  │
└─────────────────────────────────────────────────────────┘
```

### 2.2 加密算法选择

| 算法 | 用途 | 密钥长度 |
|------|------|---------|
| **AES-256-GCM** | 词典内容加密 | 256 位 |
| **RSA-2048** | 密钥加密/签名 | 2048 位 |
| **SHA-256** | 完整性校验 | 256 位 |

### 2.3 用户专属密钥生成

```python
# 云端密钥生成
import hashlib
import secrets

def generate_user_key(user_id: str, pack_id: str) -> bytes:
    """
    为每个用户 - 词典包组合生成唯一密钥
    
    密钥 = HMAC-SHA256(user_id + pack_id + server_secret)
    """
    server_secret = get_server_secret()  # 服务器端保密
    key_material = f"{user_id}:{pack_id}:{server_secret}"
    key = hashlib.sha256(key_material.encode()).digest()
    return key

def encrypt_vocabulary_pack(pack_data: dict, user_id: str, pack_id: str) -> bytes:
    """加密词典包"""
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM
    
    # 生成用户专属密钥
    key = generate_user_key(user_id, pack_id)
    aesgcm = AESGCM(key)
    
    # 生成随机 nonce
    nonce = secrets.token_bytes(12)
    
    # 加密内容
    plaintext = json.dumps(pack_data).encode()
    ciphertext = aesgcm.encrypt(nonce, plaintext, None)
    
    # 返回：nonce + ciphertext
    return nonce + ciphertext
```

### 2.4 客户端解密

```csharp
// C# 客户端解密
public class VocabularyPackDecryptor
{
    private readonly string _userId;
    private readonly HttpClient _httpClient;
    
    public async Task<VocabularyPack> DecryptPackAsync(
        string packId, 
        byte[] encryptedData)
    {
        // 1. 从云端获取用户密钥（或从本地缓存）
        var key = await GetUserKeyAsync(packId);
        
        // 2. 提取 nonce 和 ciphertext
        var nonce = encryptedData.Take(12).ToArray();
        var ciphertext = encryptedData.Skip(12).ToArray();
        
        // 3. AES-GCM 解密
        using var aesGcm = new AesGcm(key);
        var plaintext = new byte[ciphertext.Length - 16]; // 减去 tag 长度
        
        try
        {
            aesGcm.Decrypt(
                nonce, 
                ciphertext, 
                null, // 无额外认证数据
                plaintext,
                ciphertext.AsSpan(ciphertext.Length - 16, 16).ToArray() // tag
            );
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("解密失败：密钥无效或数据被篡改");
        }
        
        // 4. 反序列化
        var json = Encoding.UTF8.GetString(plaintext);
        return JsonSerializer.Deserialize<VocabularyPack>(json);
    }
    
    private async Task<byte[]> GetUserKeyAsync(string packId)
    {
        // 尝试从本地缓存获取
        if (_keyCache.TryGetValue(packId, out var cached))
        {
            return cached;
        }
        
        // 从云端获取
        var response = await _httpClient.GetAsync($"/api/v1/packs/{packId}/key");
        response.EnsureSuccessStatusCode();
        
        var key = await response.Content.ReadAsByteArrayAsync();
        
        // 缓存密钥（30 天）
        _keyCache.Set(packId, key, TimeSpan.FromDays(30));
        
        return key;
    }
}
```

### 2.5 内容混淆

为进一步增加破解难度，对词典包内容进行混淆：

```python
def obfuscate_pack(pack_data: dict) -> dict:
    """混淆词典包内容"""
    
    # 1. 打乱热词顺序
    random.shuffle(pack_data['hotwords'])
    
    # 2. 添加假数据（用于干扰）
    fake_hotwords = generate_fake_hotwords(100)
    pack_data['hotwords'].extend(fake_hotwords)
    
    # 3. 添加水印（可追踪泄露源）
    pack_data['_watermark'] = {
        'user_id': current_user_id,
        'purchase_id': purchase_id,
        'timestamp': datetime.now().isoformat()
    }
    
    # 4. 字段名混淆
    obfuscated = {}
    field_map = {
        'hotwords': 'a1b2c3',
        'shortcuts': 'd4e5f6',
        'terms': 'g7h8i9'
    }
    for orig, obs in field_map.items():
        if orig in pack_data:
            obfuscated[obs] = pack_data[orig]
    
    return obfuscated
```

---

## 3. 验证机制

### 3.1 验证流程

```
┌─────────────────────────────────────────────────────────┐
│                    词典包验证流程                        │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  用户点击安装词典包                                      │
│       ↓                                                 │
│  检查本地授权缓存                                        │
│       ↓                                                 │
│  ┌─────────────────┐                                    │
│  │ 缓存有效？      │── 是 ──→ 直接安装                 │
│  └────────┬────────                                    │
│           │ 否                                          │
│           ↓                                             │
│  联网验证购买记录                                        │
│       ↓                                                 │
│  ┌─────────────────┐                                    │
│  │ 已购买？        │── 否 ──→ 跳转购买页面             │
│  └────────┬────────┘                                    │
│           │ 是                                          │
│           ↓                                             │
│  返回授权凭证（有效期 30 天）                              │
│       ↓                                                 │
│  缓存授权凭证                                            │
│       ↓                                                 │
│  下载并安装词典包                                        │
└─────────────────────────────────────────────────────────┘
```

### 3.2 授权凭证格式

```json
{
  "license": {
    "pack_id": "medical-standard",
    "user_id": "user_123456",
    "purchase_id": "pur_789012",
    "issued_at": "2026-03-07T10:00:00Z",
    "expires_at": "2026-04-06T10:00:00Z",
    "device_limit": 3,
    "signature": "RSA 签名..."
  }
}
```

### 3.3 验证代码实现

```csharp
public class LicenseValidator
{
    private readonly LicenseService _licenseService;
    private readonly MemoryCache _cache;
    
    /// <summary>
    /// 验证词典包授权
    /// </summary>
    public async Task<bool> ValidatePackLicense(string packId)
    {
        // 1. 检查本地缓存
        var cached = _cache.Get<LicenseInfo>(packId);
        if (cached != null && cached.ExpiresAt > DateTime.Now)
        {
            return true;  // 缓存有效
        }
        
        // 2. 联网验证
        try
        {
            var response = await _licenseService.ValidateLicenseAsync(packId);
            
            if (response.Valid)
            {
                // 3. 缓存授权（30 天）
                _cache.Set(packId, new LicenseInfo
                {
                    ExpiresAt = DateTime.Now.AddDays(30),
                    PurchaseId = response.PurchaseId
                }, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                });
                
                return true;
            }
        }
        catch (HttpRequestException)
        {
            // 网络错误，检查是否有离线授权
            var offlineLicense = LoadOfflineLicense(packId);
            if (offlineLicense?.ExpiresAt > DateTime.Now)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查设备限制
    /// </summary>
    public async Task<bool> CheckDeviceLimit(string packId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/packs/{packId}/devices");
        var deviceInfo = await response.Content.ReadFromJsonAsync<DeviceInfo>();
        
        return deviceInfo.CurrentDevices < deviceInfo.MaxDevices;
    }
}

public class LicenseInfo
{
    public DateTime ExpiresAt { get; set; }
    public string PurchaseId { get; set; }
}
```

### 3.4 离线授权

对于无法联网的场景，提供离线授权机制：

```csharp
public class OfflineLicenseManager
{
    /// <summary>
    /// 生成离线授权文件
    /// </summary>
    public async Task<byte[]> GenerateOfflineLicense(
        string packId, 
        int days = 30)
    {
        // 需要用户先登录云端获取授权码
        var authCode = await _httpClient.PostAsJsonAsync("/api/v1/offline/license", new
        {
            pack_id = packId,
            machine_id = MachineId.Generate(),
            days = days
        });
        
        return await authCode.Content.ReadAsByteArrayAsync();
    }
    
    /// <summary>
    /// 验证离线授权
    /// </summary>
    public bool VerifyOfflineLicense(byte[] licenseData)
    {
        try
        {
            var license = JsonSerializer.Deserialize<OfflineLicense>(licenseData);
            
            // 验证签名
            var valid = _rsaVerifier.Verify(
                license.Data, 
                license.Signature, 
                HashAlgorithmName.SHA256, 
                RSASignaturePadding.Pkcs1);
            
            return valid && license.ExpiresAt > DateTime.Now;
        }
        catch
        {
            return false;
        }
    }
}
```

### 3.5 设备限制（可选）

```csharp
public class DeviceManager
{
    /// <summary>
    /// 注册当前设备
    /// </summary>
    public async Task<bool> RegisterDevice(string packId)
    {
        var machineId = MachineId.Generate();
        
        var response = await _httpClient.PostAsJsonAsync("/api/v1/devices/register", new
        {
            pack_id = packId,
            machine_id = machineId,
            device_name = Environment.MachineName
        });
        
        return response.IsSuccessStatusCode;
    }
    
    /// <summary>
    /// 获取已注册设备列表
    /// </summary>
    public async Task<List<Device>> GetRegisteredDevices(string packId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/packs/{packId}/devices");
        return await response.Content.ReadFromJsonAsync<List<Device>>();
    }
    
    /// <summary>
    /// 移除设备
    /// </summary>
    public async Task RemoveDevice(string packId, string deviceId)
    {
        await _httpClient.DeleteAsync($"/api/v1/packs/{packId}/devices/{deviceId}");
    }
}
```

---

## 4. VSCode 模式市场集成

### 4.1 设计理念

参考 VSCode 插件市场模式：
- 客户端内嵌"打开市场"按钮
- 点击唤起默认浏览器打开网页
- 网页浏览/购买词典包
- 购买后点击"安装到 WordFlow"
- 浏览器协议唤起客户端（wordflow://install?pack_id=xxx）
- 客户端下载并安装词典包

### 4.2 整体流程

```
┌─────────────────────────────────────────────────────────────┐
│                    词典市场购买安装流程                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  WordFlow 客户端          浏览器              云端服务器     │
│                                                             │
│  [词典市场] ──点击──► 打开网页 ──► 浏览词典包               │
│       │                    │                                │
│       │                    │ 选择购买                       │
│       │                    │───支付──►                     │
│       │                    │                                │
│       │                    │ 购买成功                       │
│       │                    │ 显示 [安装到 WordFlow] 按钮      │
│       │                    │                                │
│       │◄───点击安装───────│                                │
│       │                    │                                │
│  wordflow://install?pack_id=xxx                            │
│       │                                                     │
│       ▼                                                     │
│  验证购买 ─────────────────────────► 验证通过               │
│       │                                                     │
│       ▼                                                     │
│  下载加密词典包 ◄──────────────────────── 返回数据          │
│       │                                                     │
│       ▼                                                     │
│  解密安装                                                    │
│       │                                                     │
│       ▼                                                     │
│  安装成功，提示用户                                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 浏览器协议注册

**Windows 注册表**：
```
HKEY_CLASSES_ROOT\wordflow\
    (Default) = "URL:WordFlow Protocol"
    URL Protocol = ""
    shell\
        open\
            command\
                (Default) = "C:\Program Files (x86)\WordFlow\WordFlow.exe" "%1"
```

**安装包自动注册**：
```csharp
// 安装时注册协议
public class ProtocolRegistrar
{
    public static void RegisterWordFlowProtocol()
    {
        using var key = Registry.ClassesRoot.CreateSubKey("wordflow");
        key.SetValue("", "URL:WordFlow Protocol");
        key.SetValue("URL Protocol", "");
        
        using var commandKey = key.CreateSubKey(@"shell\open\command");
        var exePath = Process.GetCurrentProcess().MainModule.FileName;
        commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
    }
}
```

### 4.4 客户端处理协议

```csharp
// App.xaml.cs
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 处理协议启动
        if (e.Args.Length > 0)
        {
            var url = e.Args[0];
            HandleProtocol(url);
        }
    }
    
    private async void HandleProtocol(string url)
    {
        var uri = new Uri(url);
        
        if (uri.Scheme == "wordflow")
        {
            switch (uri.Host)
            {
                case "install":
                    var packId = uri.ParseQueryString()["pack_id"];
                    await InstallPackAsync(packId);
                    break;
                    
                case "activate":
                    var licenseKey = uri.ParseQueryString()["key"];
                    await ActivateLicenseAsync(licenseKey);
                    break;
            }
        }
    }
    
    private async Task InstallPackAsync(string packId)
    {
        // 验证购买
        var valid = await _licenseValidator.ValidatePackLicense(packId);
        if (!valid)
        {
            ShowPurchaseDialog(packId);
            return;
        }
        
        // 下载安装
        await _packManager.InstallAsync(packId);
        
        // 提示成功
        ShowNotification($"词典包已安装成功！");
    }
}
```

### 4.5 网页端实现

```html
<!-- 词典包详情页 -->
<div class="pack-detail">
    <h1>{{ pack.name }}</h1>
    <p>{{ pack.description }}</p>
    <div class="price">¥{{ pack.price }}</div>
    
    <!-- 未购买时显示 -->
    <button v-if="!purchased" @click="purchase">
        立即购买
    </button>
    
    <!-- 已购买显示 -->
    <button v-else @click="installToWordFlow">
        安装到 WordFlow
    </button>
</div>

<script>
export default {
    methods: {
        async purchase() {
            // 调用支付 API
            const result = await api.purchasePack(this.pack.id);
            if (result.success) {
                this.purchased = true;
            }
        },
        
        installToWordFlow() {
            // 唤起 WordFlow 客户端
            const protocolUrl = `wordflow://install?pack_id=${this.pack.id}`;
            window.location.href = protocolUrl;
            
            // 如果客户端未安装，显示提示
            setTimeout(() => {
                if (!this.clientDetected) {
                    this.showClientNotInstalled = true;
                }
            }, 1000);
        }
    }
}
</script>
```

---

## 5. 水印追踪机制

### 5.1 水印类型

| 类型 | 说明 | 用途 |
|------|------|------|
| **显性水印** | 预览内容中的版权信息 | 警示作用 |
| **隐性水印** | 嵌入内容的用户信息 | 追踪泄露源 |
| **购买水印** | 购买记录绑定 | 法律维权 |

### 5.2 隐性水印实现

```python
def embed_watermark(pack_data: dict, user_id: str, purchase_id: str) -> dict:
    """
    在词典包中嵌入隐性水印
    水印分散在多个字段中，难以完全移除
    """
    import hashlib
    
    # 生成水印 ID
    watermark_id = hashlib.sha256(
        f"{user_id}:{purchase_id}:watermark".encode()
    ).hexdigest()[:16]
    
    # 1. 在热词中添加水印（每 100 个词插入一个水印词）
    watermark_hotwords = generate_watermark_hotwords(watermark_id)
    for i, hw in enumerate(watermark_hotwords):
        if i * 100 < len(pack_data['hotwords']):
            pack_data['hotwords'].insert(i * 100, hw)
    
    # 2. 在快捷短语内容中添加不可见字符
    for shortcut in pack_data.get('shortcuts', []):
        shortcut['content'] = embed_invisible_chars(
            shortcut['content'], 
            watermark_id
        )
    
    # 3. 加密存储水印映射
    _watermark_registry[purchase_id] = {
        'user_id': user_id,
        'watermark_id': watermark_id,
        'created_at': datetime.now().isoformat()
    }
    
    return pack_data

def generate_watermark_hotwords(watermark_id: str) -> list:
    """生成水印热词（看起来像正常词）"""
    # 使用水印 ID 生成看似正常的专业术语
    return [
        {"word": f"医学术语{watermark_id[:4]}", "weight": 1, "_watermark": True},
        {"word": f"专业词汇{watermark_id[4:8]}", "weight": 1, "_watermark": True},
    ]
```

### 5.3 泄露检测

```python
def detect_leak_source(leaked_content: str) -> dict:
    """
    从泄露的内容中检测水印，追踪泄露源
    """
    # 1. 提取可能的水印词
    watermark_pattern = r'医学术语 ([a-f0-9]{4})|专业词汇 ([a-f0-9]{4})'
    matches = re.findall(watermark_pattern, leaked_content)
    
    if matches:
        # 2. 重组水印 ID
        watermark_parts = [m[0] or m[1] for m in matches]
        watermark_id = ''.join(watermark_parts)
        
        # 3. 查找购买记录
        purchase = _watermark_registry.get(watermark_id)
        if purchase:
            return {
                'found': True,
                'user_id': purchase['user_id'],
                'purchase_id': watermark_id,
                'purchase_date': purchase['created_at']
            }
    
    return {'found': False}
```

---

## 6. 法律维权

### 6.1 维权依据

1. **软件著作权**：WordFlow 软件著作权登记
2. **用户协议**：用户购买时同意的使用条款
3. **购买记录**：云端保存的完整购买日志

### 6.2 维权流程

```
发现盗版 → 取证（截图、录屏、下载）
    ↓
检测水印 → 确定泄露源
    ↓
发送律师函 → 要求停止侵权
    ↓
平台投诉 → 下架盗版内容
    ↓
诉讼 → 索赔
```

### 6.3 用户协议条款示例

```
第三条 知识产权保护

3.1 用户购买的词典包仅限个人使用，不得：
    (a) 复制、传播给他人
    (b) 公开发布、分享
    (c) 用于商业用途
    (d) 反向工程、反编译

3.2 违反上述条款的，平台有权：
    (a) 终止用户账户
    (b) 追究法律责任
    (c) 要求经济赔偿
```

---

## 7. 安全总结

### 7.1 防护措施汇总

| 措施 | 防护对象 | 实施难度 |
|------|---------|---------|
| AES-256 加密 | 文件复制 | 低 |
| 用户专属密钥 | 密钥泄露 | 中 |
| 在线验证 | 未购买使用 | 低 |
| 离线授权 | 离线场景 | 中 |
| 设备限制 | 账户共享 | 中 |
| 内容混淆 | 反编译 | 高 |
| 水印追踪 | 泄露追溯 | 中 |
| 法律维权 | 盗版传播 | - |

### 7.2 建议实施顺序

1. **第一阶段**：基础加密 + 在线验证
2. **第二阶段**：VSCode 模式市场集成
3. **第三阶段**：水印追踪 + 法律维权

### 7.3 注意事项

1. **不影响正常用户**：验证流程要快，缓存要有效
2. **提供离线方案**：考虑网络不佳的场景
3. **持续更新**：根据新威胁更新防护措施
4. **用户教育**：告知用户尊重知识产权

---

*本文档为 WordFlow 词典包防盗版机制设计，将根据实际情况持续更新。*

*最后更新：2026-03-07*
