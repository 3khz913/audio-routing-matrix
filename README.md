<div dir="rtl">

# 🎛️ mixer — مصفوفة توجيه الصوت

> **تحكم كامل في توجيه الصوت لـ Elgato Wave Link 3 من خلال واجهة مصفوفة مرئية مع دعم MIDI و StreamDock.**

---

## 📖 عن المشروع

**mixer** هو تطبيق سطح مكتب يمكّنك من التحكم في توجيه الصوت لبرنامج Elgato Wave Link 3 عبر واجهة مصفوفة مرئية. بدلاً من الواجهة التقليدية التي تتطلب النقر على كل قناة على حدة، يوفر mixer رؤية شاملة لجميع القنوات والمخاليط في شبكة واحدة، مع إمكانية التحكم عبر أجهزة MIDI الفيزيائية وأجهزة StreamDock.

### لماذا mixer؟

- Wave Link الرسمي لا يوفر واجهة مصفوفة كاملة
- التحكم بـ MIDI يمنحك دقة واحترافية في البث المباشر
- دعم أجهزة StreamDock يوفر أزراراً وشاشات قابلة للتخصيص
- واجهة Windows 11 عصرية مع دعم الثيم الفاتح والغامق واللغة العربية

---

## ✨ الميزات

| الميزة | الوصف |
|--------|-------|
| 🎚️ **مصفوفة الصوت** | شبكة كاملة: صفوف = مصادر الصوت، أعمدة = المخاليط، مع سلايدر وكتم لكل خلية |
| 🎹 **MIDI Learn** | اربط أي زر أو فيدر أو نوب في جهاز MIDI بأي سلايدر أو زر كتم في التطبيق |
| 📟 **StreamDock** | Plugin مخصص لأجهزة StreamDock/Mirabox مع أيقونات SVG ديناميكية |
| 🌗 **ثيم مزدوج** | ثيم داكن وثيم فاتح بتصميم Windows 11 العصري — التبديل لحظي |
| 🌍 **ثنائي اللغة** | دعم كامل للعربية 🇸🇦 والإنجليزية 🇬🇧 |
| 📊 **VU Meters** | مؤشرات مستوى الصوت مباشرة على كل قناة |
| 🔍 **Focused App** | عرض التطبيق النشط والقناة المرتبطة به |
| 💾 **حفظ الإعدادات** | حفظ حجم النافذة وموقعها، تصدير/استيراد تعيينات MIDI |
| 📋 **Systray** | تصغير إلى شريط النظام مع إمكانية التشغيل مع بدء التشغيل |

---

## 🏗️ البنية المعمارية

```
mixer-project/
├── server/                    # جسر Node.js بين Wave Link والتطبيق
│   ├── server.js              # WebSocket server :8765 + Wave Link SDK v2
│   └── package.json
├── mixer/                     # تطبيق WPF (.NET 10, MVVM)
│   ├── Models/                # نماذج البيانات
│   ├── Services/              # WaveLink, MIDI, الترخيص, التخزين
│   ├── ViewModels/            # منطق التطبيق (MVVM)
│   └── Views/                 # الواجهات والثيمات والترجمة
└── streamdock-plugin/         # Plugin لـ StreamDock Creator
    ├── plugin/                # كود Node.js الرئيسي
    ├── propertyInspector/     # واجهات الإعدادات
    └── static/                # الأيقونات
```

```
Wave Link 3 ←→ server.js (:8765) ←→ mixer.exe (WPF)
                                    ←→ StreamDock Plugin
                                    ←→ MIDI Devices
```

---

## 🚀 التشغيل السريع

### المتطلبات

- **Windows 10/11**
- **Elgato Wave Link 3** مثبت وشغال
- **.NET 10 SDK** (للتطوير) أو [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- **Node.js** (لتشغيل الخادم)
- **StreamDock Creator** مع جهاز StreamDock/Mirabox (اختياري)

### خطوات التشغيل

```bash
# 1. تشغيل خادم Node.js
cd server
npm install
node server.js
# ستشاهد: ✅ Connected to Wave Link 3

# 2. في نافذة طرفية أخرى — تشغيل تطبيق WPF
cd mixer
dotnet run
```

> **ملاحظة:** عند تشغيل `dotnet run`، سيقوم التطبيق تلقائياً بتشغيل خادم Node.js في الخلفية. النقطة الخضراء في أعلى اليمين تعني أن الاتصال نشط.

---

## 🎹 MIDI Learn

1. اضغط على زر ✎ بجانب أي سلايدر أو زر كتم
2. اختر جهاز MIDI من القائمة
3. اضغط **Learn** ثم حرك المقبض/الزر في جهازك
4. احفظ — وسيتم تفعيل الربط فوراً

**التعيينات تُحفظ في:** `%LocalAppData%\mixer\midi_mappings.json`

**أنواع MIDI المدعومة:**
- **CC (Control Change)** — للمقابض والفيدرات
- **Note On/Off** — للأزرار
- **التحكم:** Absolute, Relative (للمقابض اللانهائية), Toggle

---

## 📟 StreamDock Plugin

### التثبيت

انسخ مجلد `com.mixer.audio.sdPlugin` إلى:

```
%APPDATA%\HotSpot\StreamDock\plugins\
```

ثم أعد تشغيل StreamDock Creator.

### الإجراءات المتاحة

| الإجراء | الوصف |
|---------|-------|
| **Channel Level** | تحكم في مستوى قناة محددة — مع إمكانية توجيهها إلى مكس معين أو الكل |
| **Mix Level** | تحكم في مستوى مكس كامل |

---

## ⌨️ اختصارات لوحة المفاتيح

| الاختصار | الوظيفة |
|----------|---------|
| `Ctrl + M` | كتم الكل |
| `Ctrl + U` | إلغاء كتم الكل |

---

## 📁 هيكل التخزين المحلي

```
%LocalAppData%\mixer\
├── app_log.txt           # سجل الأخطاء والتشغيل
├── midi_mappings.json    # تعيينات MIDI
├── settings.json         # إعدادات المستخدم (اللغة، الثيم، بدء التشغيل)
└── window_settings.json  # حجم وموقع النافذة
```

---

## 🛠️ التقنيات المستخدمة

| التقنية | الاستخدام |
|---------|-----------|
| **.NET 10 / WPF** | تطبيق سطح المكتب |
| **Node.js** | جسر Wave Link |
| **@darrellvs/node-wave-link-sdk v2** | الاتصال بـ Wave Link 3 |
| **Melanchall.DryWetMidi** | دعم أجهزة MIDI |
| **StreamDock Plugin SDK** | دعم أجهزة StreamDock/Mirabox |
| **WebSocket** | بروتوكول الاتصال بين جميع المكونات |

---

## 📝 الرخصة

هذا المشروع مفتوح المصدر. حرية الاستخدام والتعديل.

---

</div>

---

# 🎛️ mixer — Audio Routing Matrix

> **Full control over Elgato Wave Link 3 audio routing through a visual matrix interface with MIDI and StreamDock support.**

---

## 📖 About

**mixer** is a desktop application that gives you complete control over Elgato Wave Link 3's audio routing through a visual matrix. Instead of clicking through individual channels in the standard interface, mixer provides a comprehensive grid view of all channels and mixes — with support for physical MIDI controllers and StreamDock devices.

### Why mixer?

- Wave Link lacks a full matrix view
- MIDI control gives you professional-grade precision for streaming
- StreamDock support adds customizable buttons with live feedback
- Modern Windows 11 design with dark/light themes and full Arabic localization

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🎚️ **Audio Matrix** | Full grid: rows = sources, columns = mixes — slider + mute per cell |
| 🎹 **MIDI Learn** | Bind any MIDI knob, fader, or button to any slider or mute control |
| 📟 **StreamDock** | Custom plugin with dynamic SVG icons for real-time feedback |
| 🌗 **Dual Theme** | Dark & light themes with modern Windows 11 design — instant switching |
| 🌍 **Bilingual** | Full Arabic 🇸🇦 and English 🇬🇧 support |
| 📊 **VU Meters** | Live audio level indicators per channel |
| 🔍 **Focused App** | Shows the currently active application and its routed channel |
| 💾 **Persistence** | Save window size/position, export/import MIDI mappings |
| 📋 **Systray** | Minimize to system tray, optional auto-start with Windows |

---

## 🏗️ Architecture

```
mixer-project/
├── server/                    # Node.js bridge (Wave Link ↔ WebSocket :8765)
│   ├── server.js
│   └── package.json
├── mixer/                     # WPF desktop app (.NET 10, MVVM)
│   ├── Models/                # Data models
│   ├── Services/              # WaveLink, MIDI, storage
│   ├── ViewModels/            # MVVM logic
│   └── Views/                 # UI, themes, localization
└── streamdock-plugin/         # StreamDock Creator plugin
    ├── plugin/                # Node.js plugin code
    ├── propertyInspector/     # Settings UI
    └── static/                # Icons
```

```
Wave Link 3 ←→ server.js (:8765) ←→ mixer.exe (WPF)
                                    ←→ StreamDock Plugin
                                    ←→ MIDI Devices
```

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11**
- **Elgato Wave Link 3** installed and running
- **.NET 10 SDK** (dev) or [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- **Node.js** (for the bridge server)
- **StreamDock Creator** + StreamDock/Mirabox device (optional)

### Steps

```bash
# 1. Start the Node.js bridge server
cd server
npm install
node server.js
# You should see: ✅ Connected to Wave Link 3

# 2. In another terminal — run the WPF app
cd mixer
dotnet run
```

> **Note:** When running `dotnet run`, the app will automatically launch the Node.js server as a child process. The green dot in the top-right means the connection is live.

---

## 🎹 MIDI Learn

1. Click the ✎ button next to any slider or mute control
2. Select your MIDI device from the list
3. Click **Learn**, then move the knob/press the button on your device
4. Save — the mapping is active immediately

**Mappings stored at:** `%LocalAppData%\mixer\midi_mappings.json`

**Supported MIDI types:**
- **CC (Control Change)** — knobs and faders
- **Note On/Off** — buttons
- **Control modes:** Absolute, Relative (endless encoders), Toggle

---

## 📟 StreamDock Plugin

### Installation

Copy the `com.mixer.audio.sdPlugin` folder to:

```
%APPDATA%\HotSpot\StreamDock\plugins\
```

Then restart StreamDock Creator.

### Available Actions

| Action | Description |
|--------|-------------|
| **Channel Level** | Control a specific channel's volume — optionally routed to a specific mix or all mixes |
| **Mix Level** | Control an entire mix's master volume |

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl + M` | Mute All |
| `Ctrl + U` | Unmute All |

---

## 📁 Local Storage

```
%LocalAppData%\mixer\
├── app_log.txt           # Application logs
├── midi_mappings.json    # MIDI mappings
├── settings.json         # User settings (language, theme, startup)
└── window_settings.json  # Window position and size
```

---

## 🛠️ Tech Stack

| Technology | Purpose |
|------------|---------|
| **.NET 10 / WPF** | Desktop application |
| **Node.js** | Wave Link bridge server |
| **@darrellvs/node-wave-link-sdk v2** | Wave Link 3 communication |
| **Melanchall.DryWetMidi** | MIDI device support |
| **StreamDock Plugin SDK** | StreamDock/Mirabox device support |
| **WebSocket** | Communication protocol between all components |

---

## 📝 License

This project is open source. Free to use and modify.
