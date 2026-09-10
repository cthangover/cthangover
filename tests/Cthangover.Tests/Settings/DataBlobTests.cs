using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class DataBlobTests
    {
        [Fact]
        public void Default_VersionIsOne()
        {
            var blob = new DataBlob();
            Assert.Equal(1, blob.Version);
        }

        [Fact]
        public void Constructor_WithVersion()
        {
            var blob = new DataBlob(5);
            Assert.Equal(5, blob.Version);
        }

        [Fact]
        public void GetInt_ExistingKey_ReturnsValue()
        {
            var blob = new DataBlob();
            blob.SetInt("x", 42);
            Assert.Equal(42, blob.GetInt("x"));
        }

        [Fact]
        public void GetInt_MissingKey_ReturnsDefault()
        {
            var blob = new DataBlob();
            Assert.Equal(0, blob.GetInt("nope"));
            Assert.Equal(-1, blob.GetInt("nope", -1));
        }

        [Fact]
        public void GetFloat_ExistingKey_ReturnsValue()
        {
            var blob = new DataBlob();
            blob.SetFloat("x", 3.14f);
            Assert.Equal(3.14f, blob.GetFloat("x"));
        }

        [Fact]
        public void GetFloat_MissingKey_ReturnsDefault()
        {
            var blob = new DataBlob();
            Assert.Equal(0f, blob.GetFloat("nope"));
            Assert.Equal(1f, blob.GetFloat("nope", 1f));
        }

        [Fact]
        public void GetString_ExistingKey_ReturnsValue()
        {
            var blob = new DataBlob();
            blob.SetString("x", "hello");
            Assert.Equal("hello", blob.GetString("x"));
        }

        [Fact]
        public void GetString_MissingKey_ReturnsDefault()
        {
            var blob = new DataBlob();
            Assert.Equal("", blob.GetString("nope"));
            Assert.Equal("fallback", blob.GetString("nope", "fallback"));
        }

        [Fact]
        public void GetBool_ExistingKey_ReturnsValue()
        {
            var blob = new DataBlob();
            blob.SetBool("x", true);
            Assert.True(blob.GetBool("x"));
            blob.SetBool("x", false);
            Assert.False(blob.GetBool("x"));
        }

        [Fact]
        public void GetBool_MissingKey_ReturnsDefault()
        {
            var blob = new DataBlob();
            Assert.False(blob.GetBool("nope"));
            Assert.True(blob.GetBool("nope", true));
        }

        [Fact]
        public void WriteJson_ThenReadJson_Roundtrip()
        {
            var blob = new DataBlob();
            var original = new { X = 42, Y = "test" };
            blob.WriteJson(original);
            var restored = blob.ReadJson<object>();
            Assert.NotNull(restored);
        }

        [Fact]
        public void WriteJson_ThenReadJson_Typed_Roundtrip()
        {
            var blob = new DataBlob();
            blob.WriteJson(12345);
            Assert.Equal(12345, blob.ReadJson<int>());
        }

        [Fact]
        public void ReadJson_EmptyPayload_ReturnsNew()
        {
            var blob = new DataBlob();
            Assert.Equal(0, blob.ReadJson<int>());
        }

        [Fact]
        public void ReadJson_InvalidJson_ReturnsNew()
        {
            var blob = new DataBlob();
            blob.SetBytes(new byte[] { 1, 2, 3 });
            Assert.Equal(0, blob.ReadJson<int>());
        }

        [Fact]
        public void GetBytes_FlushesDictToJson()
        {
            var blob = new DataBlob();
            blob.SetInt("x", 42);
            var bytes = blob.GetBytes();
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void SetBytes_ThenGetBytes_Roundtrip()
        {
            var blob = new DataBlob();
            var data = new byte[] { 10, 20, 30 };
            blob.SetBytes(data);
            Assert.Equal(data, blob.GetBytes());
        }

        [Fact]
        public void ConstructWithData_ReadJson()
        {
            var blob = new DataBlob(1, 999);
            Assert.Equal(999, blob.ReadJson<int>());
        }

        [Fact]
        public void ConstructWithBytes_GetBytes()
        {
            var data = new byte[] { 5, 6, 7 };
            var blob = new DataBlob(data);
            Assert.Equal(data, blob.GetBytes());
        }

        [Fact]
        public void WriteJson_Null_ClearsPayload()
        {
            var blob = new DataBlob();
            blob.SetInt("x", 1);
            blob.WriteJson<object>(null);
            Assert.Empty(blob.GetBytes());
            Assert.Equal(-1, blob.GetInt("x", -1));
        }

        [Fact]
        public void SetBytes_Null_EmptyArray()
        {
            var blob = new DataBlob();
            blob.SetBytes(null);
            Assert.Empty(blob.GetBytes());
        }

        [Fact]
        public void KeyValue_Persistence_AcrossDictFlush()
        {
            var blob = new DataBlob();
            blob.SetInt("a", 10);
            blob.SetString("b", "world");
            var json = System.Text.Encoding.UTF8.GetString(blob.GetBytes());

            var blob2 = new DataBlob(System.Text.Encoding.UTF8.GetBytes(json));
            Assert.Equal(10, blob2.GetInt("a"));
            Assert.Equal("world", blob2.GetString("b"));
        }

        [Fact]
        public void SetString_Null_StoredAsEmpty()
        {
            var blob = new DataBlob();
            blob.SetString("x", null);
            Assert.Equal("", blob.GetString("x"));
        }
    }
}
