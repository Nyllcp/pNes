using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper2 : Mapper
    {

        private int prgBankNo;

        public override void WriteCart(int address, byte data)
        {
            if (address < 0x2000)
            {
                if (_rom.chrRamEnabled)
                {
                    _rom.chrRom[address & (_rom.chrRom.Length - 1)] = data;
                }
            }
            else if (address < 0x3F00)
            {
                WriteVram(address, data);
            }
            else if (address < 0x8000 && _rom.batteryRam)
            {
                prgRam[address & prgRamBankSize - 1] = data;
            }
            else
            {
                prgBankNo = data & 0xF;
            }
        }

        protected override byte ReadPRG(int address)
        {
            if(address < 0xC000)
            {
                return _rom.prgRom[(address & (prgRomBankSize16k - 1)) + (prgBankNo * prgRomBankSize16k)];
            }
            else
            {
                return _rom.prgRom[(address & (prgRomBankSize16k - 1)) + (_rom.prgRom.Length - prgRomBankSize16k)];
            }  
        }

    }
}
