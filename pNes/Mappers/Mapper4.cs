using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper4 : Mapper
    {

        private int[] bankRegigster = new int[8];
        private bool prgBankMode8000 = false;
        private bool chr2kHigh = false;
        private int bankWriteSelect;

        private bool irqEnabled = false;
        private int irqLatch = 0xFF;
        private int irqCounter;
        private int lastAddress;

        public override void WriteCart(int address, byte data)
        {
            if (address < 0x2000)
            {
                WriteCHR(address, data);
            }
            else if (address < 0x3F00)
            {
                WriteVram(address, data);
            }
            else if (address < 0x8000)
            {
                prgRam[address & prgRamBankSize - 1] = data;
            }
            else
            {
                WriteReg(address, data);
            }
        }

        private void WriteReg(int address, byte data)
        {
            if(address < 0xA000)
            {
                if((address & 1) == 0)
                {
                    chr2kHigh = (data & 0x80) == 0x80;
                    prgBankMode8000 = (data & 0x40) == 0x40;
                    bankWriteSelect = data & 0x7;
                }
                else
                {
                    int value = data;
                    if(bankWriteSelect <= 5)
                    {
                        value %= chr1kBankCount;
                    }
                    else
                    {
                        //data &= 0x3F;
                        value %= prg8kBankCount;
                    }
                    bankRegigster[bankWriteSelect] = value;
                }
            }
            else if(address < 0xC000)
            {
                if ((address & 1) == 0) verticalMirroring = (data & 1) == 0;
                //ram controll, ram always on for now
            }
            else if(address < 0xE000)
            {
                if ((address & 1) != 0)
                {
                    irqCounter = 0;
                }
                else
                    irqLatch = data;
            }
            else // e000-FFFF
            {
                if ((address & 1) != 0)
                    irqEnabled = true;
                else
                {
                    irqEnabled = false;
                    iflag = false;
                }
                    
            }

        }


        protected override byte ReadPRG(int address)
        {
            int kAddress = address & 0x1FFF;
            if(prgBankMode8000)
            {
                if (address < 0xA000)
                {
                    return _rom.prgRom[kAddress + (_rom.prgRom.Length - prgRomBankSize16k)];
                }
                else if (address < 0xC000)
                {
                    return _rom.prgRom[kAddress + (bankRegigster[7] * prgRomBankSize8k)];
                }
                else if(address < 0xE000)
                {
                    return _rom.prgRom[kAddress + (bankRegigster[6] * prgRomBankSize8k)];
                }
                else
                {
                    return _rom.prgRom[kAddress + (_rom.prgRom.Length -  prgRomBankSize8k)];
                }
            }
            else
            {
                if (address < 0xA000)
                {
                    return _rom.prgRom[kAddress + (bankRegigster[6] * prgRomBankSize8k)];
                }
                else if (address < 0xC000)
                {
                    return _rom.prgRom[kAddress + (bankRegigster[7] * prgRomBankSize8k)];
                }
                else if (address < 0xE000)
                {
                    return _rom.prgRom[kAddress + (_rom.prgRom.Length - prgRomBankSize16k)];
                }
                else
                {
                    return _rom.prgRom[kAddress + (_rom.prgRom.Length - prgRomBankSize8k)];
                }
            }
            

        }

        protected override byte ReadCHR(int address)
        {
            if (((lastAddress >> 12) & 1) == 0 && ((address >> 12) & 1) != 0)
            {
                if(irqCounter <= 0)
                {
                    irqCounter = irqLatch;
                }
                else
                {
                    irqCounter--;
                    if(irqCounter == 0 && irqEnabled)
                    {
                        iflag = true;
                    }
                }
            }
            lastAddress = address;
            int kAdress = address & 0x3FF;
            if(chr2kHigh)
            {
                switch ((address >> 10) & 0x7)
                {
                    case 0: return _rom.chrRom[kAdress + (bankRegigster[2] * chrRomBankSize1k)];
                    case 1: return _rom.chrRom[kAdress + (bankRegigster[3] * chrRomBankSize1k)];
                    case 2: return _rom.chrRom[kAdress + (bankRegigster[4] * chrRomBankSize1k)];
                    case 3: return _rom.chrRom[kAdress + (bankRegigster[5] * chrRomBankSize1k)];
                    case 4: return _rom.chrRom[kAdress + ((bankRegigster[0] & 0xFE) * chrRomBankSize1k)];
                    case 5: return _rom.chrRom[kAdress + ((bankRegigster[0] | 1) * chrRomBankSize1k)];
                    case 6: return _rom.chrRom[kAdress + ((bankRegigster[1] & 0xFE) * chrRomBankSize1k)];
                    case 7: return _rom.chrRom[kAdress + ((bankRegigster[1] | 1) * chrRomBankSize1k)];
                    default: return 0;
                }
            }
            else
            {
                switch ((address >> 10) & 0x7)
                {
                    case 0: return _rom.chrRom[kAdress + ((bankRegigster[0] & 0xFE) * chrRomBankSize1k)];
                    case 1: return _rom.chrRom[kAdress + ((bankRegigster[0] | 1) * chrRomBankSize1k)];
                    case 2: return _rom.chrRom[kAdress + ((bankRegigster[1] & 0xFE) * chrRomBankSize1k)];
                    case 3: return _rom.chrRom[kAdress + ((bankRegigster[1] | 1) * chrRomBankSize1k)];
                    case 4: return _rom.chrRom[kAdress + (bankRegigster[2] * chrRomBankSize1k)];
                    case 5: return _rom.chrRom[kAdress + (bankRegigster[3] * chrRomBankSize1k)];
                    case 6: return _rom.chrRom[kAdress + (bankRegigster[4] * chrRomBankSize1k)];
                    case 7: return _rom.chrRom[kAdress + (bankRegigster[5] * chrRomBankSize1k)];
                    default: return 0;
                }
            }
            
        }
    }
}
