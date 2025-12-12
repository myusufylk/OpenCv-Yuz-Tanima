# 👤 C# EmguCV Yüz Tanıma ve Kayıt Sistemi

Bu proje, C# ve **Emgu CV** kütüphanesi kullanılarak geliştirilmiş gerçek zamanlı bir yüz tanıma uygulamasıdır. Uygulama, web kamerasından görüntü alır, yüzleri tespit eder (Haar Cascade), kullanıcıları veri setine kaydeder ve **LBPH (Local Binary Patterns Histograms)** algoritması ile kayıtlı yüzleri anlık olarak tanır.

## 🚀 Özellikler

* **Gerçek Zamanlı Yüz Tespiti:** Haar Cascade sınıflandırıcısı kullanarak görüntüdeki yüzleri anlık olarak çerçeve içine alır.
* **Yüz Tanıma (LBPH):** Kaydedilmiş yüzleri %80 doğruluk eşiği (Threshold) ile ayırt eder ve ismini ekrana yazar.
* **Veri Seti Oluşturma:** Kameradan alınan yüz görüntülerini kırparak `dataset` klasörüne otomatik olarak kaydeder.
* **Dinamik Eğitim:** Yeni bir yüz kaydedildiğinde model otomatik olarak yeniden eğitilir; uygulamayı kapatıp açmaya gerek yoktur.
* **Otomatik İsimlendirme:** Kayıt edilen resimler `isim_index.jpg` formatında saklanır, böylece etiketleme sorunu yaşanmaz.

## 🛠️ Kullanılan Teknolojiler

* **Dil:** C# (Windows Forms)
* **Görüntü İşleme:** Emgu.CV (OpenCV Wrapper)
* **Algoritma:** LBPHFaceRecognizer (Yüz Tanıma), Haar Cascade (Yüz Tespiti)
* **Veri Yönetimi:** Dosya tabanlı (.jpg) veri seti yönetimi.

## 📦 Kurulum ve Hazırlık

Projeyi sorunsuz çalıştırmak için aşağıdaki adımları dikkatlice uygulayın.

### 1. Kütüphanelerin Yüklenmesi
Proje **Emgu.CV** kütüphanesine bağımlıdır. Visual Studio'da **NuGet Package Manager** konsolunu açın ve aşağıdaki paketleri yükleyin:

```powershell
Install-Package Emgu.CV
Install-Package Emgu.CV.runtime.windows
Install-Package Emgu.CV.Bitmap
