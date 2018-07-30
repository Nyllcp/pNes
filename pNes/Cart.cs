using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;


namespace pNes
{
    class Cart
    {


        private byte[] prgRom;
        private byte[] chrRom;
        private byte[] prgRam;
        private byte[,] ppuRam = new byte[4, ppuRamBankSize];
        private Mapper1 _mapper1;

        private const int prgRomBankSize16k = 0x4000;
        private const int chrRomBankSize8k = 0x2000;
        private const int prgRomBankSize8k = 0x2000;
        private const int chrRomBankSize4k = 0x1000;
        private const int prgRamBankSize = 0x2000;
        private const int ppuRamBankSize = 0x400;

        private int mapperNumber = 0;

        private bool verticalMirroring = false;
        private bool chrRamEnabled = false;
        private bool prgRamEnabled = false;
        private bool trainer = false;
        private bool ignoreMirroring = false;

        private int selectedBank = 0;

        public Cart()
        {

        }

        public bool LoadRom(string fileName)
        {
            BinaryReader _reader;
            MemoryStream _ms = new MemoryStream();

            bool success = false;
            if (Path.GetExtension(fileName) == ".zip")
            {
                using (var _zip = ZipFile.OpenRead(fileName))
                {
                    foreach (var entry in _zip.Entries)
                    {
                        if (Path.GetExtension(entry.Name) == ".nes")
                        {
                            using (var stream = entry.Open())
                            {
                                stream.CopyTo(_ms);
                            }
                            break;
                        }    
                    }
                    _reader = new BinaryReader(_ms);
                }
            }
            else if (Path.GetExtension(fileName) == ".nes")
            {
                _reader = new BinaryReader(File.Open(fileName, FileMode.Open));
            }
            else
            {
                return false;
            }

            _reader.BaseStream.Seek(0, SeekOrigin.Begin);
            if (_reader.ReadByte() == 'N' &&
                _reader.ReadByte() == 'E' &&
                _reader.ReadByte() == 'S' &&
                _reader.ReadByte() == 0x1A)
                {
                int prgRomInfo = _reader.ReadByte();
                int prgRomSize = prgRomInfo != 0 ? prgRomBankSize16k * prgRomInfo : prgRomBankSize16k;
                prgRom = new byte[prgRomSize];
                int chrRomInfo = _reader.ReadByte();
                if (chrRomInfo == 0)
                {
                    chrRamEnabled = true;
                    chrRomInfo = chrRomBankSize8k;
                }
                else
                {
                    chrRomInfo = chrRomInfo * chrRomBankSize8k;
                }
                chrRom = new byte[chrRomInfo];
                byte flags6 = _reader.ReadByte();
                byte flags7 = _reader.ReadByte();
                byte prgRamSize = _reader.ReadByte();

                verticalMirroring = (flags6 & 1) != 0;
                prgRamEnabled = ((flags6 >> 1) & 1) != 0;
                prgRam = prgRamSize == 0 ? new byte[prgRamBankSize] : new byte[prgRamBankSize * prgRamSize];
                trainer = ((flags6 >> 2) & 1) != 0;
                ignoreMirroring = ((flags6 >> 3) & 1) != 0;
                //ppuRam = ignoreMirroring ? new byte[0x1000] : null;
                mapperNumber = (flags6 >> 4) | (flags7 & 0xF0);

                int prgStartByte = trainer ? 0x210 : 0x10;

                _reader.BaseStream.Seek(prgStartByte, SeekOrigin.Begin);

                for (int i = 0; i < prgRom.Length; i++)
                {
                    prgRom[i] = _reader.ReadByte();
                }
                for (int i = 0; i < chrRom.Length; i++)
                {
                    if (chrRamEnabled) break;
                    chrRom[i] = _reader.ReadByte();
                }
                if(mapperNumber == 1)
                {
                    _mapper1 = new Mapper1(prgRom, chrRom);
                }


                success = true;
            }
            else
            {
                success = false;
            }
            _ms.Close();
            _reader.Close();
            return success;
        }
        public byte ReadCart(int address)
        {
            if(mapperNumber == 1)
            {
                return _mapper1.ReadCart(address);
            }
            if(mapperNumber == 2)
            {
                if (address < 0x4000)
                {
                    return PpuRead(address);
                }
                if (address < 0xC000)
                {
                    return prgRom[(prgRomBankSize16k * selectedBank) + (address & (prgRomBankSize16k - 1))];
                }
                else
                {
                    return prgRom[(prgRom.Length - prgRomBankSize16k) + (address & (prgRomBankSize16k - 1))];
                }
            }
            if(address < 0x4000)
            {
                return PpuRead(address);
            }
            else
            {
                return prgRom[address & (prgRom.Length - 1)];
            }
           
        }
        public void WriteCart(int address, byte data)
        {
            //cart writes, todo
            if(mapperNumber == 1)
            {
                _mapper1.WriteCart(address, data);
            }
            if (mapperNumber == 2)
            {
                if (address > 0x7FFF)
                {
                    selectedBank = data & 0xF;
                }
            }

            if (address < 0x4000)
            {
                PpuWrite(address, data);
            }

            
        }

        public void Tick()
        {
            if (_mapper1 != null) _mapper1.Tick();
        }

        private byte PpuRead(int address)
        {
            if (address < 0x2000)
            {
                return chrRom[address & (chrRom.Length - 1)];
            }
            else if (address < 0x3F00)
            {
                if (verticalMirroring)
                { 
                    switch ((address >> 10) & 3)
                    {
                        case 0: return ppuRam[0, address & ppuRamBankSize - 1]; 
                        case 1: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 2: return ppuRam[0, address & ppuRamBankSize - 1]; 
                        case 3: return ppuRam[1, address & ppuRamBankSize - 1]; 
                    }
                }
                else
                {
                    switch ((address >> 10) & 3)
                    {
                        case 0: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 1: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 2: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 3: return ppuRam[1, address & ppuRamBankSize - 1];
                    }
                }
            }
            return 0;
        }

        private void PpuWrite(int address, byte data)
        {
            if (address < 0x2000)
            {
                if(chrRamEnabled)
                {
                    chrRom[address] = data;
                }
            }
            else if(address < 0x3F00)
            {
                if(verticalMirroring)
                {
                    switch ((address >> 10) & 3)
                    {
                        case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 1: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 2: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    }
                }
                else
                {
                    switch ((address >> 10) & 3)
                    {
                        case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 1: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 2: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    }
                }
             
            }
          
        }

    }
}
