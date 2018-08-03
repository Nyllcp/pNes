using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace pNes
{
    class Rom
    {
        public byte[] prgRom;
        public byte[] chrRom;

        public int prgRomCount;
        public int chrRomCount;
        
        public bool verticalMirroring = false;
        public bool ignoreMirroring = false;
        public bool trainer = false;
        public bool chrRamEnabled = false;
        public bool batteryRam = false;

        public int mapperNumber = 0;

        private const int prgRomBankSize16k = 0x4000;
        private const int chrRomBankSize8k = 0x2000;
        private const int prgRamBankSize = 0x2000;

        public Rom() { }

        public bool Load(string fileName)
        {
            MemoryStream ms = new MemoryStream();
            BinaryReader reader;


            if (Path.GetExtension(fileName) == ".zip")
            {
                using (var _zip = ZipFile.OpenRead(fileName))
                {
                    foreach (var entry in _zip.Entries)
                    {
                        if (Path.GetExtension(entry.Name) == ".nes")
                        {
                            ms = new MemoryStream();
                            using (var stream = entry.Open())
                            {
                                stream.CopyTo(ms);
                            }
                            break;
                        }
                    }
                }
                reader = new BinaryReader(ms);
            }
            else
            {
                reader = new BinaryReader(File.Open(fileName, FileMode.Open));
            }

            
            if(reader.BaseStream.Length < 16)
            {
                reader.Close();
                return false;
            }
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            byte[] header = new byte[0x10];
            reader.Read(header, 0, header.Length);

            if (header[0] != 'N' &&
                header[1] != 'E' &&
                header[2] != 'S' &&
                header[3] != 0x1A)
            {
                reader.Close();
                return false;
            }



            prgRomCount = header[4] == 0 ? 1 : header[4];
            chrRomCount = header[5];
            chrRamEnabled = chrRomCount == 0 ? true : false;

            verticalMirroring = (header[6] & 1) != 0;
            batteryRam = ((header[6] >> 1) & 1) != 0;
            trainer = ((header[6] >> 2) & 1) != 0;
            ignoreMirroring = ((header[6] >> 3) & 1) != 0;
            mapperNumber = (header[6] >> 4) | (header[7] & 0xF0);


            prgRom = new byte[prgRomCount * prgRomBankSize16k];
            if (!chrRamEnabled) chrRom = new byte[chrRomCount * chrRomBankSize8k];
            else chrRom = new byte[chrRomBankSize8k];
            int startAdress = trainer ? 0x10 + 0x200 : 0x10;
            reader.BaseStream.Seek(startAdress, SeekOrigin.Begin);
            
            reader.Read(prgRom, 0, prgRom.Length);
            if(!chrRamEnabled)
            {
                reader.Read(chrRom, 0, chrRom.Length);
            }

            reader.Close();

            return true;

         
        }
    }
}
