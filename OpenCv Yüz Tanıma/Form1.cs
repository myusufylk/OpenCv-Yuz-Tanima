using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml.Linq;

namespace OpenCVYuzTanima
{
    public partial class MainForm : Form
    {
        // Kamera ve görüntü işleme değişkenleri
        private VideoCapture _camera;
        private Mat _frame = new Mat();
        private CascadeClassifier _faceCascade;

        // Yüz tanıma
        private LBPHFaceRecognizer _recognizer;
        private readonly Dictionary<int, string> _labelToName = new Dictionary<int, string>();
        private readonly Dictionary<string, int> _nameToLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private bool _recognizerReady = false;

        // Klasör ve dosya ayarları
        private string _datasetDir;
        private string _cascadePath;

        // Çalışma parametreleri
        private readonly Size _trainSize = new Size(100, 100);
        private const double RecognizeThreshold = 80.0; // result.Distance < 80 ise kabul

        public MainForm()
        {
            InitializeComponent();
            // dataset klasörünü oluştur
            _datasetDir = Path.Combine(Application.StartupPath, "dataset");
            Directory.CreateDirectory(_datasetDir);

            // Haar cascade yolu (assets klasöründen kopyalanıyor)
            _cascadePath = Path.Combine(Application.StartupPath, "assets", "haarcascade_frontalface_default.xml");

            // Cascade dosya kontrolü
            if (!File.Exists(_cascadePath))
            {
                MessageBox.Show("haarcascade_frontalface_default.xml bulunamadı. assets klasörünü kontrol edin.", "Dosya Eksik", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                try
                {
                    _faceCascade = new CascadeClassifier(_cascadePath);
                }
                catch (Exception ex)
                {
                    _faceCascade = null;
                    MessageBox.Show(
                        "Yüz tespit modeli yüklenemedi. Lütfen assets/haarcascade_frontalface_default.xml dosyasını geçerli bir içerikle değiştirin.\nDetay: " + ex.Message,
                        "Model Yükleme Hatası",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }

            // Başlangıçta tanıyıcıyı eğit
            TryTrainRecognizer();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (_camera == null)
                {
                    if (!TryOpenCamera())
                    {
                        MessageBox.Show("Herhangi bir kameraya erişilemedi. Lütfen kamera izinlerini ve sürücüleri kontrol edin.", "Kamera Açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                _camera.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kamera başlatılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool TryOpenCamera()
        {
            // Denenecek API'ler ve cihaz indeksleri
            var apis = new[] { VideoCapture.API.DShow, VideoCapture.API.Msmf, VideoCapture.API.Any };
            var indices = new[] { 0, 1, 2 };

            foreach (var idx in indices)
            {
                foreach (var api in apis)
                {
                    try
                    {
                        _camera = new VideoCapture(idx, api);
                        if (_camera != null && _camera.IsOpened)
                        {
                            try
                            {
                                _camera.Set(CapProp.FrameWidth, 640);
                                _camera.Set(CapProp.FrameHeight, 480);
                            }
                            catch { }
                            _camera.ImageGrabbed += Camera_ImageGrabbed;
                            return true;
                        }
                        _camera?.Dispose();
                        _camera = null;
                    }
                    catch
                    {
                        try { _camera?.Dispose(); } catch { }
                        _camera = null;
                    }
                }
            }
            return false;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                if (_camera != null)
                {
                    _camera.ImageGrabbed -= Camera_ImageGrabbed;
                    _camera.Stop();
                    _camera.Dispose();
                    _camera = null;
                }

                // Son görüntüyü temizle
                if (pictureBoxFrame.Image != null)
                {
                    pictureBoxFrame.Image.Dispose();
                    pictureBoxFrame.Image = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kamera durdurulamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Camera_ImageGrabbed(object sender, EventArgs e)
        {
            try
            {
                if (_camera == null) return;

                _camera.Retrieve(_frame);
                if (_frame == null || _frame.IsEmpty)
                    return;

                using (var frameImage = _frame.ToImage<Bgr, byte>())
                {
                    // Aynaya göre ters (yatay) yansıt
                    CvInvoke.Flip(frameImage, frameImage, FlipType.Horizontal);

                    using (var gray = frameImage.Convert<Gray, byte>())
                    {
                        // Yüz tespiti
                        var faces = new Rectangle[0];
                        if (_faceCascade != null)
                        {
                            faces = _faceCascade.DetectMultiScale(
                                gray,
                                1.1,
                                5,
                                new Size(50, 50),
                                Size.Empty
                            );
                        }

                        foreach (var face in faces)
                        {
                            // Yüz ROI
                            var faceGray = gray.GetSubRect(face).Resize(_trainSize.Width, _trainSize.Height, Inter.Cubic);

                            string labelText = "Bilinmiyor";
                            if (_recognizerReady)
                            {
                                try
                                {
                                    var result = _recognizer.Predict(faceGray);
                                    if (result.Label != -1 && result.Distance < RecognizeThreshold && _labelToName.TryGetValue(result.Label, out var name))
                                    {
                                        labelText = name;
                                    }
                                }
                                catch { /* tanıyıcı hazır değilse sessizce geç */ }
                            }

                            // Dikdörtgen çiz
                            CvInvoke.Rectangle(frameImage, face, new MCvScalar(0, 255, 0), 2);
                            // İsim yaz
                            CvInvoke.PutText(
                                frameImage,
                                labelText,
                                new Point(face.X, Math.Max(0, face.Y - 8)),
                                FontFace.HersheySimplex,
                                0.8,
                                new MCvScalar(0, 255, 0),
                                2
                            );
                        }

                        // Görüntüyü PictureBox'a aktar (Imencode ile Bitmap'e dönüştür) ve UI thread'ine marshal et
                        using (var vb = new VectorOfByte())
                        {
                            CvInvoke.Imencode(".jpg", frameImage, vb);
                            var data = vb.ToArray();
                            this.BeginInvoke((Action)(() =>
                            {
                                using (var ms = new MemoryStream(data))
                                using (var bmp = new Bitmap(ms))
                                {
                                    var old = pictureBoxFrame.Image;
                                    pictureBoxFrame.Image = (Bitmap)bmp.Clone();
                                    if (old != null) old.Dispose();
                                }
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Sürekli tetiklenen event; sadece uyarı göster ve devam et
                Console.WriteLine("Frame işleme hatası: " + ex.Message);
            }
        }

        private void btnSaveFace_Click(object sender, EventArgs e)
        {
            try
            {
                // İsim alma (TextBox)
                var name = txtName.Text?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Lütfen bir isim girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Dosya adı için güvenli isim
                name = SanitizeName(name);

                if (_frame == null || _frame.IsEmpty)
                {
                    MessageBox.Show("Kameradan görüntü alınamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var frameImage = _frame.ToImage<Bgr, byte>())
                using (var gray = frameImage.Convert<Gray, byte>())
                {
                    if (_faceCascade == null)
                    {
                        MessageBox.Show("Yüz tespit modeli yüklenmedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var faces = _faceCascade.DetectMultiScale(gray, 1.1, 5, new Size(50, 50), Size.Empty);
                    if (faces == null || faces.Length == 0)
                    {
                        MessageBox.Show("Kaydedilecek yüz bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    // Birden fazla yüz varsa ilkini kaydedelim; istersen tümünü kaydedebilirim
                    var face = faces[0];
                    var faceGray = gray.GetSubRect(face).Resize(_trainSize.Width, _trainSize.Height, Inter.Cubic);

                    // Sıradaki index'i bul
                    var nextIndex = GetNextIndexForName(name);
                    var fileName = $"{name}_{nextIndex}.jpg";
                    var fullPath = Path.Combine(_datasetDir, fileName);

                    faceGray.Save(fullPath);

                    MessageBox.Show($"Yüz kaydedildi: {fullPath}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Yeni verilerle yeniden eğit
                    TryTrainRecognizer();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yüz kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TryTrainRecognizer()
        {
            try
            {
                TrainRecognizerFromDataset();
                _recognizerReady = true;
            }
            catch (Exception ex)
            {
                _recognizerReady = false;
                MessageBox.Show("Tanıyıcı eğitilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TrainRecognizerFromDataset()
        {
            var files = Directory.Exists(_datasetDir)
                ? Directory.GetFiles(_datasetDir, "*.jpg")
                : Array.Empty<string>();

            var images = new List<Image<Gray, byte>>();
            var labels = new List<int>();

            _labelToName.Clear();
            _nameToLabel.Clear();

            int nextLabel = 0;

            foreach (var f in files)
            {
                var fn = Path.GetFileNameWithoutExtension(f) ?? string.Empty;
                // Beklenen format: name_index
                var underscore = fn.LastIndexOf('_');
                if (underscore <= 0) continue;
                var name = fn.Substring(0, underscore);
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!_nameToLabel.TryGetValue(name, out var label))
                {
                    label = nextLabel++;
                    _nameToLabel[name] = label;
                    _labelToName[label] = name;
                }

                try
                {
                    using (var img = new Image<Gray, byte>(f))
                    {
                        var resized = img.Resize(_trainSize.Width, _trainSize.Height, Inter.Cubic);
                        images.Add(resized);
                        labels.Add(label);
                    }
                }
                catch
                {
                    // Hatalı görsel varsa atla
                }
            }

            if (images.Count == 0 || labels.Count == 0)
            {
                _recognizer = null;
                _recognizerReady = false;
                return;
            }

            _recognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 80);
            // Not: LBPH parametreleri: radius, neighbors, grid_x, grid_y, threshold
            // Burada threshold düşük tutulabilir; yine biz Distance ile kontrol ediyoruz.
            _recognizer.Train(images.Select(i => i.Mat).ToArray(), labels.ToArray());
        }

        private int GetNextIndexForName(string name)
        {
            var pattern = name + "_";
            var files = Directory.GetFiles(_datasetDir, name + "_*.jpg");
            int max = 0;
            foreach (var f in files)
            {
                var fn = Path.GetFileNameWithoutExtension(f);
                if (fn != null && fn.StartsWith(pattern))
                {
                    var tail = fn.Substring(pattern.Length);
                    if (int.TryParse(tail, out var idx))
                    {
                        if (idx > max) max = idx;
                    }
                }
            }
            return max + 1;
        }

        private static string SanitizeName(string name)
        {
            // yalnızca a-z, 0-9 ve alt çizgi
            name = name.ToLowerInvariant();
            name = Regex.Replace(name, "[^a-z0-9_]+", "_");
            name = name.Trim('_');
            if (string.IsNullOrWhiteSpace(name)) name = "user";
            return name;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try
            {
                if (_camera != null)
                {
                    _camera.ImageGrabbed -= Camera_ImageGrabbed;
                    _camera.Stop();
                    _camera.Dispose();
                    _camera = null;
                }

                if (pictureBoxFrame.Image != null)
                {
                    pictureBoxFrame.Image.Dispose();
                    pictureBoxFrame.Image = null;
                }
            }
            catch { }
        }
    }
}