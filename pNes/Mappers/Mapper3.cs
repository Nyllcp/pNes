using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper3 :Mapper
    {
        private int chrBankNo;

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
            else if (address < 0x8000 && _rom.batteryRam)
            {
                prgRam[address & prgRamBankSize - 1] = data;
            }
            else
            {
                chrBankNo = data & 0x3;
            }
        }

        protected override byte ReadCHR(int address)
        {
            return _rom.chrRom[(address & (chrRomBankSize8k - 1)) + (chrBankNo * chrRomBankSize8k)];
        }
    }
}
