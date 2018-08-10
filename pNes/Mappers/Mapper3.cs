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


        protected override void WritePRG(int address, byte data)
        {
            chrBankNo = data & 0x3;
        }

        protected override byte ReadCHR(int address)
        {
            return _rom.chrRom[(address & (chrRomBankSize8k - 1)) + (chrBankNo * chrRomBankSize8k)];
        }

        public override void WriteSaveState(ref Savestate state)
        {
            state.chrBankNo = chrBankNo;
            base.WriteSaveState(ref state);
        }
        public override void LoadSaveState(ref Savestate state)
        {
            chrBankNo = state.chrBankNo;
            base.LoadSaveState(ref state);
        }
    }
}
