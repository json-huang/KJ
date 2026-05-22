using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

static Guid GetTagId(string tagKey)
{
    var ns = Guid.Parse("a3c9d4d9-3d6a-4b44-9a51-1d0aa2a1c0a5");
    var nsBytes = ns.ToByteArray();
    (nsBytes[0], nsBytes[3]) = (nsBytes[3], nsBytes[0]);
    (nsBytes[1], nsBytes[2]) = (nsBytes[2], nsBytes[1]);
    (nsBytes[4], nsBytes[5]) = (nsBytes[5], nsBytes[4]);
    (nsBytes[6], nsBytes[7]) = (nsBytes[7], nsBytes[6]);

    var nameBytes = Encoding.UTF8.GetBytes(tagKey);
    var data = new byte[nsBytes.Length + nameBytes.Length];
    Buffer.BlockCopy(nsBytes, 0, data, 0, nsBytes.Length);
    Buffer.BlockCopy(nameBytes, 0, data, nsBytes.Length, nameBytes.Length);

    var hash = SHA1.HashData(data);
    var newGuid = new byte[16];
    Array.Copy(hash, 0, newGuid, 0, 16);
    newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
    newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

    (newGuid[0], newGuid[3]) = (newGuid[3], newGuid[0]);
    (newGuid[1], newGuid[2]) = (newGuid[2], newGuid[1]);
    (newGuid[4], newGuid[5]) = (newGuid[5], newGuid[4]);
    (newGuid[6], newGuid[7]) = (newGuid[7], newGuid[6]);

    return new Guid(newGuid);
}

var tagKey = args.Length > 0 ? args[0] : "test.temp";
var tagId = GetTagId(tagKey);
Console.WriteLine($"TagKey: {tagKey}");
Console.WriteLine($"TagId:  {tagId}");

var connStr = "Server=localhost;Port=3306;Database=mesdb;Uid=root;Pwd=root;SslMode=none;Charset=utf8mb4;";
using var conn = new MySqlConnection(connStr);
await conn.OpenAsync();

// 确保 Device 存在（Address 是 Owned Type，列名带前缀）
var deviceId = Guid.Parse("8b2a1ce1-39cf-4e2a-8c4a-2a38f7b1f2d0");
await using (var devCmd = new MySqlCommand(
    "INSERT IGNORE INTO Devices (Id, Name, Description, Type, State, LastConnected, Address_Host, Address_Port, PropertiesJson) " +
    "VALUES (@id, '模拟设备', '测试用模拟设备', 0, 0, @now, '127.0.0.1', 502, '{}')", conn))
{
    devCmd.Parameters.AddWithValue("@id", deviceId.ToString());
    devCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
    await devCmd.ExecuteNonQueryAsync();
}

// 确保 Tag 存在
await using (var tagCmd = new MySqlCommand(
    "INSERT IGNORE INTO Tags (Id, DeviceId, Name, DataType, Address, Quality, Timestamp, Direction) " +
    "VALUES (@id, @devId, @name, 4, 'HR0', 0, @now, 0)", conn))
{
    tagCmd.Parameters.AddWithValue("@id", tagId.ToString());
    tagCmd.Parameters.AddWithValue("@devId", deviceId.ToString());
    tagCmd.Parameters.AddWithValue("@name", tagKey);
    tagCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
    await tagCmd.ExecuteNonQueryAsync();
}

var now = DateTime.UtcNow;
for (int i = 0; i < 50; i++)
{
    var ts = now.AddMinutes(-50 + i);
    var value = 50 + 30 * Math.Sin(i * 0.2);
    await using var cmd = new MySqlCommand(
        "INSERT INTO TagHistory (Id, TagId, Timestamp, Value, Quality) VALUES (@id, @tagId, @ts, @val, 0)", conn);
    cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
    cmd.Parameters.AddWithValue("@tagId", tagId.ToString());
    cmd.Parameters.AddWithValue("@ts", ts);
    cmd.Parameters.AddWithValue("@val", value.ToString("F2"));
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine($"已插入 50 条测试数据，标签: {tagKey}");
