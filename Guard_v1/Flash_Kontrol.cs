using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace svchost
{
    public partial class Flash_Kontrol : Form
    {
        public Flash_Kontrol()
        {
            InitializeComponent();
        }

        public static bool ikinciKontrol;

        public static void FlashKontrol(bool kontrol)
        {
            ikinciKontrol = kontrol;
        }

        private void Flash_Kontrol_Load(object sender, EventArgs e)
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            int x = bounds.Width - (this.Width);
            bounds = Screen.PrimaryScreen.Bounds;
            int y = bounds.Height / 100;
            this.Location = new Point(x, y);
            PictureBox p = new PictureBox();
            this.Controls.Add(p);

            if (ikinciKontrol)
            {
                p.Dock = DockStyle.Fill;
                p.SizeMode = PictureBoxSizeMode.StretchImage;
                p.Image = guard.Properties.Resources.cancel_512px;
            }
            else
            {
                p.Dock = DockStyle.Fill;
                p.SizeMode = PictureBoxSizeMode.StretchImage;
                p.Image = guard.Properties.Resources.checked_512px;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
