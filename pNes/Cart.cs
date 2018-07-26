using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace pNes
{
    class Cart
    {

        private byte[] prgRom;
        private byte[] chrRom;
        private byte[] prgRam;
        private byte[,] ppuRam = new byte[4,0x400];

        private const int prgRomBankSize = 0x4000;
        private const int chrRomBankSize = 0x2000;
        private const int prgRamBankSize = 0x2000;

        private int mapperNumber = 0;

        private bool verticalMirroring = false;
        private bool chrRamEnabled = false;
        private bool prgRamEnabled = false;
        private bool trainer = false;
        private bool ignoreMirroring = false;

        private int selctedBank = 0;

        public Cart()
        {

        }

        public bool LoadRom(string fileName)
        {
            bool success = false;
            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                if (reader.ReadByte() == 'N' &&
                    reader.ReadByte() == 'E' &&
                    reader.ReadByte() == 'S' &&
                    reader.ReadByte() == 0x1A)
                {
                    int prgRomInfo = reader.ReadByte();
                    int prgRomSize = prgRomInfo != 0 ? prgRomBankSize * prgRomInfo : prgRomBankSize;
                    prgRom = new byte[prgRomSize];
                    int chrRomInfo = reader.ReadByte();
                    if(chrRomInfo == 0)
                    {
                        chrRamEnabled = true;
                        chrRomInfo = chrRomBankSize;
                    }
                    else
                    {
                        chrRomInfo = chrRomInfo * chrRomBankSize;
                    }
                    chrRom = new byte[chrRomInfo];
                    byte flags6 = reader.ReadByte();
                    byte flags7 = reader.ReadByte();
                    byte prgRamSize = reader.ReadByte();

                    verticalMirroring = (flags6 & 1) != 0;
                    prgRamEnabled =  ((flags6 >> 1) & 1) != 0;
                    prgRam = prgRamSize == 0 ? new byte[prgRamBankSize] : new byte[prgRamBankSize * prgRamSize];
                    trainer = ((flags6 >> 2) & 1) != 0;
                    ignoreMirroring = ((flags6 >> 3) & 1) != 0;
                    //ppuRam = ignoreMirroring ? new byte[0x1000] : null;
                    mapperNumber = (flags6 >> 4) | (flags7 & 0xF0);

                    int prgStartByte = trainer ? 0x210 : 0x10;

                    reader.BaseStream.Seek(prgStartByte, SeekOrigin.Begin);

                    for(int i = 0; i < prgRom.Length; i++)
                    {
                        prgRom[i] = reader.ReadByte();
                    }
                    for (int i = 0; i < chrRom.Length; i++)
                    {
                        if (chrRamEnabled) break;
                        chrRom[i] = reader.ReadByte();
                    }


                    success = true;
                }
                else
                {
                    success = false;
                }
            }

            return success;
        }
        public byte ReadCart(int address)
        {
            if(mapperNumber == 2)
            {
                if(address < 0xC000)
                {
                    return prgRom[(prgRomBankSize * selctedBank) + (address & (prgRomBankSize - 1))];
                }
                else
                {
                    return prgRom[(prgRom.Length - prgRomBankSize) + (address & (prgRomBankSize - 1))];
                }
            }
            return prgRom[address & (prgRom.Length -1)];
        }
        public void WriteCart(int address, byte data)
        {
            
            //cart writes, todo
            if(mapperNumber == 2)
            {
                if(address > 0x7FFF)
                {
                    selctedBank = data & 0xF;
                }
            }
        }

        public byte PpuRead(int address)
        {
            return chrRom[address & (chrRom.Length - 1)];    
        }

        public void PpuWrite(int address, byte data)
        {
            if (address < 0x2000)
            {
                if(chrRamEnabled)
                {
                    chrRom[address] = data;
                }
            }
          
        }

    }
}
