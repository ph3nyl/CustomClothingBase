using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomClothingBase;
public class CustomDatReader : DatReader
{
    public byte[] Buffer { get; set; }
    //All that is required is the Buffer to be set
    public CustomDatReader(byte[] buffer) : base(String.Empty, 0, 0, 0) { 
        Buffer = buffer;
    }
    public CustomDatReader(string datFilePath, uint offset, uint size, uint blockSize) : base(datFilePath, offset, size, blockSize) { }
    public CustomDatReader(FileStream stream, uint offset, uint size, uint blockSize) : base(stream, offset, size, blockSize) { }
}


public class Foo
{
    public Foo(string foo) { }
}
public class Bar : Foo
{
    public Bar():base(null) { }
    public Bar(string foo) : base(foo)
    {
    }
}