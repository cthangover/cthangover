using Cthangover.Core.Scenes.TscnParsing;

namespace Engine.Tests.Scenes
{
    public class TscnPropertyParserTests
    {
        [Fact]
        public void Parse_Null()
        {
            Assert.Null(TscnPropertyParser.Parse("null"));
        }

        [Fact]
        public void Parse_Bool_True()
        {
            Assert.Equal(true, TscnPropertyParser.Parse("true"));
        }

        [Fact]
        public void Parse_Bool_False()
        {
            Assert.Equal(false, TscnPropertyParser.Parse("false"));
        }

        [Fact]
        public void Parse_Int()
        {
            Assert.Equal(42, TscnPropertyParser.Parse("42"));
            Assert.Equal(-5, TscnPropertyParser.Parse("-5"));
        }

        [Fact]
        public void Parse_Float()
        {
            Assert.Equal(1.0f, TscnPropertyParser.Parse("1.0"));
            Assert.Equal(-0.5f, TscnPropertyParser.Parse("-0.5"));
        }

        [Fact]
        public void Parse_String()
        {
            Assert.Equal("hello world", TscnPropertyParser.Parse("\"hello world\""));
        }

        [Fact]
        public void Parse_ExtResource()
        {
            var result = TscnPropertyParser.Parse("ExtResource(\"1_win\")");
            Assert.IsType<TscnPropertyValue>(result);
            var pv = (TscnPropertyValue)result;
            Assert.True(pv.IsExtResourceRef);
            Assert.Equal("1_win", pv.RefId);
        }

        [Fact]
        public void Parse_SubResource()
        {
            var result = TscnPropertyParser.Parse("SubResource(\"style_1\")");
            Assert.IsType<TscnPropertyValue>(result);
            var pv = (TscnPropertyValue)result;
            Assert.True(pv.IsSubResourceRef);
            Assert.Equal("style_1", pv.RefId);
        }

        [Fact]
        public void Parse_NodePath()
        {
            var result = TscnPropertyParser.Parse("NodePath(\"Content/LootTitle\")");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("NodePath", pgv.TypeName);
            Assert.Equal("Content/LootTitle", pgv.Arg(0));
        }

        [Fact]
        public void Parse_Vector2()
        {
            var result = TscnPropertyParser.Parse("Vector2(1, 2)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Vector2", pgv.TypeName);
            Assert.Equal(2, pgv.Args.Count);
            Assert.Equal(1f, pgv.Arg(0));
            Assert.Equal(2f, pgv.Arg(1));
        }

        [Fact]
        public void Parse_Vector3()
        {
            var result = TscnPropertyParser.Parse("Vector3(1, 2, 3)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Vector3", pgv.TypeName);
            Assert.Equal(3, pgv.Args.Count);
            Assert.Equal(1f, pgv.Arg(0));
            Assert.Equal(2f, pgv.Arg(1));
            Assert.Equal(3f, pgv.Arg(2));
        }

        [Fact]
        public void Parse_Color()
        {
            var result = TscnPropertyParser.Parse("Color(0.1, 0.2, 0.3, 0.4)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Color", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
            Assert.Equal(0.1f, pgv.Arg(0));
            Assert.Equal(0.2f, pgv.Arg(1));
            Assert.Equal(0.3f, pgv.Arg(2));
            Assert.Equal(0.4f, pgv.Arg(3));
        }

        [Fact]
        public void Parse_Color_ThreeArgs()
        {
            var result = TscnPropertyParser.Parse("Color(0, 0, 0)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Color", pgv.TypeName);
            Assert.Equal(3, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Rect2()
        {
            var result = TscnPropertyParser.Parse("Rect2(0, 0, 100, 200)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Rect2", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
            Assert.Equal(0f, pgv.Arg(0));
            Assert.Equal(0f, pgv.Arg(1));
            Assert.Equal(100f, pgv.Arg(2));
            Assert.Equal(200f, pgv.Arg(3));
        }

        [Fact]
        public void Parse_Transform2D()
        {
            var result = TscnPropertyParser.Parse("Transform2D(1, 0, 0, 1, 0, 0)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Transform2D", pgv.TypeName);
            Assert.Equal(6, pgv.Args.Count);
        }

        [Fact]
        public void Parse_PackedStringArray()
        {
            var result = TscnPropertyParser.Parse("PackedStringArray(\"a\", \"b\")");
            Assert.IsType<string[]>(result);
            var arr = (string[])result;
            Assert.Equal(2, arr.Length);
            Assert.Equal("a", arr[0]);
            Assert.Equal("b", arr[1]);
        }

        [Fact]
        public void Parse_PackedInt32Array()
        {
            var result = TscnPropertyParser.Parse("PackedInt32Array(1, 2, 3)");
            Assert.IsType<int[]>(result);
            Assert.Equal(3, ((int[])result).Length);
        }

        [Fact]
        public void Parse_PackedFloat32Array()
        {
            var result = TscnPropertyParser.Parse("PackedFloat32Array(1.5, 2.5)");
            Assert.IsType<float[]>(result);
            Assert.Equal(2, ((float[])result).Length);
        }

        [Fact]
        public void Parse_PackedVector2Array()
        {
            var result = TscnPropertyParser.Parse("PackedVector2Array(1, 2, 3, 4)");
            Assert.IsType<ParsedGodotValue[]>(result);
            var arr = (ParsedGodotValue[])result;
            Assert.Equal(2, arr.Length);
            Assert.Equal("Vector2", arr[0].TypeName);
        }

        [Fact]
        public void Parse_StringName()
        {
            var result = TscnPropertyParser.Parse("StringName(\"my_name\")");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("StringName", pgv.TypeName);
            Assert.Equal("my_name", pgv.Arg(0));
        }

        [Fact]
        public void Parse_Vector2i()
        {
            var result = TscnPropertyParser.Parse("Vector2i(1, 2)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Vector2i", pgv.TypeName);
            Assert.Equal(2, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Vector3i()
        {
            var result = TscnPropertyParser.Parse("Vector3i(1, 2, 3)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Vector3i", pgv.TypeName);
            Assert.Equal(3, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Vector4()
        {
            var result = TscnPropertyParser.Parse("Vector4(1, 2, 3, 4)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Vector4", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Vector4i()
        {
            var result = TscnPropertyParser.Parse("Vector4i(1, 2, 3, 4)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Vector4i", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Rect2i()
        {
            var result = TscnPropertyParser.Parse("Rect2i(0, 0, 100, 200)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Rect2i", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Plane()
        {
            var result = TscnPropertyParser.Parse("Plane(0, 0, 0, 0)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Plane", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
        }

        [Fact]
        public void Parse_Quaternion()
        {
            var result = TscnPropertyParser.Parse("Quaternion(0, 0, 0, 1)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("Quaternion", pgv.TypeName);
            Assert.Equal(4, pgv.Args.Count);
        }

        [Fact]
        public void Parse_AABB()
        {
            var result = TscnPropertyParser.Parse("AABB(0, 0, 0, 1, 1, 1)");
            Assert.IsType<ParsedGodotValue>(result);
            var pgv = (ParsedGodotValue)result;
            Assert.Equal("AABB", pgv.TypeName);
            Assert.Equal(6, pgv.Args.Count);
        }

        [Fact]
        public void Parse_PackedInt64Array()
        {
            var result = TscnPropertyParser.Parse("PackedInt64Array(1, 2, 3)");
            Assert.IsType<long[]>(result);
            Assert.Equal(3, ((long[])result).Length);
        }

        [Fact]
        public void Parse_PackedFloat64Array()
        {
            var result = TscnPropertyParser.Parse("PackedFloat64Array(1.5, 2.5)");
            Assert.IsType<double[]>(result);
            Assert.Equal(2, ((double[])result).Length);
        }

        [Fact]
        public void Parse_PackedByteArray()
        {
            var result = TscnPropertyParser.Parse("PackedByteArray(0, 128, 255)");
            Assert.IsType<byte[]>(result);
            Assert.Equal(3, ((byte[])result).Length);
        }

        [Fact]
        public void Parse_PackedVector3Array()
        {
            var result = TscnPropertyParser.Parse("PackedVector3Array(1, 2, 3, 4, 5, 6)");
            Assert.IsType<ParsedGodotValue[]>(result);
            var arr = (ParsedGodotValue[])result;
            Assert.Equal(2, arr.Length);
            Assert.Equal("Vector3", arr[0].TypeName);
        }

        [Fact]
        public void Parse_PackedColorArray()
        {
            var result = TscnPropertyParser.Parse("PackedColorArray(1, 0, 0, 1, 0, 1, 0, 1)");
            Assert.IsType<ParsedGodotValue[]>(result);
            var arr = (ParsedGodotValue[])result;
            Assert.Equal(2, arr.Length);
            Assert.Equal("Color", arr[0].TypeName);
        }
    }
}
