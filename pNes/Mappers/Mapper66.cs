using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper66 : Mapper
    {

        private int prgBankNo;
        private int chrBankNo;


        protected override void WritePRG(int address, byte data)
        {
            chrBankNo = (data & 0x3) % chr8kBankCount;
            prgBankNo = ((data >> 4) & 0x3) % prg32kBankCount;
        }

        protected override byte ReadPRG(int address)
        {
            address &= prgRomBankSize32k - 1;
            return _rom.prgRom[address + (prgBankNo * prgRomBankSize32k)];

        }

        protected override byte ReadCHR(int address)
        {
            address &= chrRomBankSize8k - 1;
            return _rom.chrRom[address + (chrBankNo * chrRomBankSize8k)];
        }

        public override void WriteSaveState(ref Savestate state)
        {
            state.chrBankNo = chrBankNo;
            state.prgBankNo = prgBankNo;
            base.WriteSaveState(ref state);
        }
        public override void LoadSaveState(ref Savestate state)
        {
            chrBankNo = state.chrBankNo;
            prgBankNo = state.prgBankNo;
            base.LoadSaveState(ref state);
        }
    }
}
