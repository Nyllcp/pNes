using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using SFML.Audio;

namespace pNes
{
    public partial class Main : Form
    {
        private RenderWindow _window;
        private Texture _texture;
        private Sprite _sprite;
        private DrawingSurface _drawingSurface;
        private Clock _clock;
        // private Audio _audio;
        private OpenFileDialog _ofd;
        private Core _nes;


        const int nesWidth = 256;
        const int nesHeight = 240;

        private int frames;
        private int curentTime;
        private int lastTime;

        private byte[] _frame = new byte[nesWidth * nesHeight * 4]; //4 Bytes per pixel

        private bool run = false;

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            _drawingSurface = new DrawingSurface();
            _drawingSurface.Size = new System.Drawing.Size(nesWidth * 4, nesHeight * 4);
            //_drawingSurface.ContextMenuStrip = rightClickMenu;
            this.ClientSize = new Size(_drawingSurface.Right, _drawingSurface.Bottom + menuStrip.Height + statusStrip.Height);
            Controls.Add(_drawingSurface);
            _drawingSurface.Location = new System.Drawing.Point(0, menuStrip.Height);


            _ofd = new OpenFileDialog();
            //ofd.InitialDirectory = Environment.CurrentDirectory;
            _ofd.Filter = "Supported files (*.nes *.zip)|*.nes;*.zip|All files (*.*)|*.*";
            _ofd.FilterIndex = 1;
            _ofd.RestoreDirectory = true;
            InitSFML();
        }

        private void InitSFML()
        {
            _clock = new Clock();
            //_audio = new Audio();
            _texture = new Texture(nesWidth, nesHeight);
            _texture.Smooth = false;
            _sprite = new Sprite(_texture);
            _sprite.Scale = new Vector2f(4f, 4f);
            _window = new RenderWindow(_drawingSurface.Handle);
            _window.SetFramerateLimit(0);
            _texture.Update(_frame);
            _window.Clear();
            _window.Draw(_sprite);
            _window.Display();

        }

        private void RunEmulation()
        {
            run = true;
            while(run)
            {
                _drawingSurface.Select();
                _nes.RunOneFrame();
               
                UpdateFrameRGB(_nes.Frame);
                _texture.Update(_frame);
                _window.Clear();
                _window.Draw(_sprite);

                System.Windows.Forms.Application.DoEvents(); // handle form events
                _window.DispatchEvents(); // handle SFML events - NOTE this is still required when SFML is hosted in another wi

                _window.Display();

                frames++;
                curentTime = _clock.ElapsedTime.AsMilliseconds();
                if (curentTime - lastTime > 1000)
                {
                    //toolStripStatusFps.Text = "FPS: " + frames.ToString() + " Cpu Cycles Per Frame : " + cyclesperframe.ToString() + " Bytes in audio buffer:" + _audio.GetBufferedBytes();
                    toolStripStatusFps.Text = "FPS: " + frames.ToString();
                    frames = 0;
                    lastTime = _clock.ElapsedTime.AsMilliseconds();
                }
            }
        }

        private void UpdateFrameRGB(uint[] gbFrame)
        {
            for (int i = 0; i < gbFrame.Length; i++)
            {
                _frame[i * 4 + 2] = (byte)(gbFrame[i] & 0xFF);
                _frame[i * 4 + 1] = (byte)((gbFrame[i] >> 8) & 0xFF);
                _frame[i * 4 + 0] = (byte)((gbFrame[i] >> 16) & 0xFF);
                //_frame[i * 4 + 3] = (byte)((gbFrame[i] >> 24) & 0xFF);
                _frame[i * 4 + 3] = (byte)0xFF;
            }
        }


        public class DrawingSurface : System.Windows.Forms.Control
        {
            protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
            {
                // don't call base.OnPaint(e) to prevent forground painting
                // base.OnPaint(e);
            }
            protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs pevent)
            {
                // don't call base.OnPaintBackground(e) to prevent background painting
                //base.OnPaintBackground(pevent);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if (_gameboy != null) _gameboy.WriteSave();
            if (_ofd.ShowDialog() == DialogResult.OK)
            {
                run = false;
                _nes = new Core();
                if (_nes.LoadRom(_ofd.FileName))
                {

                    this.Text = "pNes - " + System.IO.Path.GetFileName(_ofd.FileName);
                    _clock.Restart().AsMilliseconds();
                    RunEmulation();
                }
                else
                {
                    this.Text = "pGameBoy - " + "Invalid ROM / File";
                }
            }
        }

  

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            run = false;
            Application.Exit();
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            run = false;
            Application.Exit();
        }
    }
}
