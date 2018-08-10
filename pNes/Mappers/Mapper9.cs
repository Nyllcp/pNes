using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper9 : Mapper
    {
        private int prgBankNo;
        private int chrFd0;
        private int chrFe0 = 4;
        private int chrFd1;
        private int chrFe1;

        private int latch0 = 0xFE;
        private int latch1 = 0xFE;

        private int lastAddress;


        protected override void WritePRG(int address, byte data)
        {
            if (address < 0xA000)
            {
                return;
            }
            if (address < 0xB000)
            {
                prgBankNo = (data & 0xF) % prg8kBankCount;
            }
            else if (address < 0xC000)
            {
                chrFd0 = data & 0x1f;
            }
            else if (address < 0xD000)
            {
                chrFe0 = data & 0x1f;
            }
            else if (address < 0xE000)
            {
                chrFd1 = data & 0x1f;
            }
            else if(address < 0xF000)// e000-FFFF
            {
                chrFe1 = data & 0x1f;
            }
            else // F000-FFFF
            {
                verticalMirroring = (data & 1) == 0;
            }
        }

        protected override byte ReadCHR(int address)
        {

            if (lastAddress == 0x0FD8)
            {
                latch0 = 0xFD;
            }
            if (lastAddress == 0x0FE8)
            {
                latch0 = 0xFE;
            }
            if (lastAddress >= 0x1FD8 && lastAddress <= 0x1FDF)
            {
                latch1 = 0xFD;
            }
            if (lastAddress >= 0x1FE8 && lastAddress <= 0x1FEF)
            {
                latch1 = 0xFE;
            }
            lastAddress = address;
            if(address < 0x1000)
            {
                address &= chrRomBankSize4k - 1;
                if (latch0 == 0xFD)
                {
                    return _rom.chrRom[address + (chrFd0 * chrRomBankSize4k)];
                }
                else
                {
                    return _rom.chrRom[address + (chrFe0 * chrRomBankSize4k)];
                }
                
            }
            else
            {
                address &= chrRomBankSize4k - 1;
                if (latch1 == 0xFD)
                {
                    return _rom.chrRom[address + (chrFd1 * chrRomBankSize4k)];
                }
                else
                {
                    return _rom.chrRom[address + (chrFe1 * chrRomBankSize4k)];
                }
            }
        }

        protected override byte ReadPRG(int address)
        {
            
            if(address < 0xA000)
            {
                address &= prgRomBankSize8k - 1;
                return _rom.prgRom[address + (prgBankNo * prgRomBankSize8k)];
            }
            else
            {
                address &= (prgRomBankSize8k * 3) - 1;
                return _rom.prgRom[address + (_rom.prgRom.Length - (prgRomBankSize8k * 3))];
            }
        }
    }

    
}
