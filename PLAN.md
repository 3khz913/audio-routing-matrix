# خطة تطوير مشروع mixer

---

## ١. منهجية العمل (Workflow)

### دورة التطوير لكل مهمة
```
١. فهم ← ٢. تعديل ← ٣. اختبار ← ٤. توثيق
```

| الخطوة | الإجراء |
|--------|---------|
| **فهم** | قراءة الكود الحالي، فهم مسار البيانات من البداية للنهاية |
| **تعديل** | تغيير أقل عدد ممكن من الملفات، ملف واحد كل مرة |
| **اختبار** | تشغيل الخادم (`npm start`) والتطبيق (`dotnet run`) بعد كل تغيير |
| **توثيق** | تحديث `README.md` أو `PLAN.md` إذا لزم |

### قاعدة ذهبية
> **نغير شيء واحد، نختبره، ثم ننتقل للشيء اللي بعده.**
> ما نكوم نغير ٥ ملفات مرة واحدة ونحوس إذا فيه خطأ.

---

## ٢. مبدأ تدفق البيانات (Data Flow)

```
Wave Link 3 ──(SDK v2)──> server.js ──(WS :8765)──> WPF Client
                            ↑                            │
                            └─── أوامر (JSON) ───────────┘
```

### JSON Contract بين الخادم والعميل

**الخادم ← العميل (state):**
```json
{
  "type": "state",
  "data": {
    "inputs": [{ "id": "sys", "name": "System", "volume": 80, "isMuted": false }],
    "mixes":  [{ "id": "pm",  "name": "Personal Mix", "isMuted": false }],
    "cells":  [{ "inputId": "sys", "mixId": "pm", "volume": 75, "isMuted": false }]
  }
}
```

**العميل → الخادم (commands):**
```json
{ "type": "setInputVolume",     "inputId": "sys", "volume": 80 }
{ "type": "setInputMixVolume",  "inputId": "sys", "mixId": "pm", "volume": 75 }
{ "type": "setInputMute",       "inputId": "sys", "isMuted": true }
{ "type": "setInputMixMute",    "inputId": "sys", "mixId": "pm", "isMuted": true }
{ "type": "setMixMute",         "mixId": "pm", "isMuted": true }
```

---

## ٣. خطة المراحل (Phases)

---

### 🟢 المرحلة الأولى: سد الثغرات (Bug Fixes) — الآن

الهدف: جعل كل ما هو موجود يعمل **بشكل صحيح**.

| # | المهمة | الملفات المتأثرة |
|---|--------|-----------------|
| 1.1 | إصلاح: الخادم يتعامل مع `setInputMixMute` | `server/server.js` |
| 1.2 | إصلاح: الخادم يتعامل مع `setMixMute` | `server/server.js` |
| 1.3 | إصلاح: `buildState` يرسل `isMuted` في mixes | `server/server.js` |
| 1.4 | إصلاح: `buildState` يرسل `isMuted` في cells | `server/server.js` |

**النتيجة:** كل الأوامر الخمسة التي يرسلها العميل تصبح مدعومة. كل الحالات تُبث بشكل كامل مع `isMuted`.

---

### 🟡 المرحلة الثانية: MIDI Learn للمكونات المفقودة

الهدف: تغطية جميع عناصر التحكم في الواجهة بـ MIDI Learn.

| # | المهمة | الملفات المتأثرة |
|---|--------|-----------------|
| 2.1 | إضافة زر MIDI Learn على Master volume/mute لكل مصدر | `MainWindow.xaml`, `SourceViewModel.cs`, `MainViewModel.cs` |
| 2.2 | إضافة زر MIDI Learn على كتم المكس (العمود) | `MainWindow.xaml`, `MixViewModel.cs`, `MainViewModel.cs` |
| 2.3 | حفظ/استعادة التعيينات الجديدة في `MidiMappingStorage` | `MidiMappingStorage.cs`, `MainViewModel.cs` |

**النتيجة:** كل سلايدر وكل زر كتم في الواجهة يقبل MIDI Learn (وليس فقط الخلايا).

---

### 🟠 المرحلة الثالثة: تحسينات الاستقرار

الهدف: البرنامج ما يطيح أبداً، يتعامل مع كل السيناريوهات.

| # | المهمة |
|---|--------|
| 3.1 | الخادم يعيد محاولة الاتصال إذا Wave Link مو شغال (مع counter للمحاولات) |
| 3.2 | الخادم يتعامل مع انقطاع Wave Link المفاجئ ويعيد الاتصال تلقائياً |
| 3.3 | تبليغ المستخدم في الواجهة إذا Wave Link غير موجود (status text واضح) |
| 3.4 | التحقق من صحة JSON القادم من الخادم (null checks, missing fields) |
| 3.5 | Heartbeat بين العميل والخادم (ping/pong) |
| 3.6 | `server.js` ما يطيح إذا `buildState` فشل (try/catch + broadcast state فاضي) |

**النتيجة:** البرنامج resistant لأي انقطاع أو خطأ غير متوقع.

---

### 🔵 المرحلة الرابعة: ميزات متقدمة (Nice-to-have)

| # | المهمة |
|---|--------|
| 4.1 | VU Meters — مؤشرات مستوى الصوت حية في الواجهة |
| 4.2 | Focused App Routing — كشف التطبيق النشط وتوجيهه لقناة |
| 4.3 | واجهة صغيرة للتحكم بـ Wave Mic (gain, mic/PC blend) |
| 4.4 | Mute All / Unmute All أزرار سريعة |
| 4.5 | تصدير/استيراد MIDI mappings |
| 4.6 | Systray icon + minimize to tray |
| 4.7 | حفظ حجم وموضع النافذة |
| 4.8 | Keyboard shortcuts |

---

## ٤. قواعد الكود (Code Conventions)

### الخادم (server.js)
- `require` في الأعلى (ما نستخدم import/ESM)
- سنيك كيس للدوال المحلية: `buildState`, `broadcast`
- كل كود غير متزامن نغلفه بـ try/catch
- لا نضيف مكتبات جديدة إلا للضرورة القصوى

### العميل (mixer/ - C#)
- **MVVM صارم**: الكود في ViewModel فقط، View تبقى رقيقة (Thin)
- Properties تبدأ بحرف كبير (PascalCase)
- `SetField` لتغيير الخصائص (ينادي `PropertyChanged` تلقائياً)
- `_updatingFromService` flag لمنع إعادة إرسال التغييرات القادمة من الخادم
- كل async method تنتهي بـ `Async`
- الأخطاء تسجل عبر `Logger.Error/Warn/Info` فقط
- UI thread marshalling عبر `Application.Current.Dispatcher`

### JSON Keys
- **دائماً lowercase** بين الخادم والعميل
- C# يستخدم `PropertyNameCaseInsensitive = true`

### القيم (Values)
- داخل الخادم (SDK v2): `0–1` float
- بين الخادم والعميل: `0–100` double (محولة في `buildState`)
- واجهة WPF: `0–100`

---

## ٥. استراتيجية الاختبار

### اختبار يدوي لكل تغيير

**الخادم:**
```bash
cd server
npm install    # مرة وحدة أول شي
npm start      # (أو node server.js)
```

**العميل:**
```bash
cd mixer
dotnet build   # تأكد إنه يبني بدون أخطاء
dotnet run     # شغّل التطبيق
```

### نقاط الفحص (Checklist) بعد كل تغيير

- [ ] الخادم يشتغل ويتصل بـ Wave Link؟ (تشوف ✅ Connected)
- [ ] التطبيق يبني بدون أخطاء؟ (`dotnet build`)
- [ ] التطبيق يفتح ويتصل بالخادم؟ (الدائرة خضراء)
- [ ] البيانات تظهر في الواجهة؟ (مصادر، مخاليط، خلايا)
- [ ] السلايدر يغير الصوت فعلاً في Wave Link؟
- [ ] أزرار الكتم تشتغل؟
- [ ] إذا سويت تغيير في Wave Link، ينعكس في الواجهة؟
- [ ] إذا طفيت الخادم، التطبيق يعيد الاتصال تلقائياً؟

---

## ٦. هيكل المشروع — مرجع سريع

```
mixer-project/
├── PLAN.md                   ← هذا الملف
├── README.md                 ← توثيق المشروع
├── .gitignore
├── server/
│   ├── server.js             ← الخادم (كل المنطق هنا)
│   ├── package.json          ← ws + node-wave-link-sdk
│   └── node_modules/
└── mixer/
    ├── mixer.csproj          ← .NET 10, WPF, DryWetMidi
    ├── App.xaml.cs           ← بداية التطبيق، بدء الخادم والخدمات
    ├── MainWindow.xaml/.cs   ← الواجهة الرئيسية
    ├── Models/
    │   ├── WaveLinkModels.cs ← ServerMessage, StateData, InputDto, MixDto, CellDto
    │   └── MidiMapping.cs   ← MidiMessageKind, ControlType, MidiAction, MidiMapping
    ├── Services/
    │   ├── Logger.cs         ← تسجيل الأخطاء
    │   ├── WaveLinkService.cs← WebSocket client للخادم
    │   ├── MidiService.cs    ← DryWetMidi wrapper
    │   └── MidiMappingStorage.cs ← حفظ/تحميل تعيينات MIDI
    ├── ViewModels/
    │   ├── MainViewModel.cs  ← المنسق الرئيسي
    │   ├── SourceViewModel.cs← صف مصدر صوت
    │   ├── MixViewModel.cs   ← عمود مزيج
    │   ├── CellViewModel.cs  ← خلية (تقاطع مصدر×مزيج)
    │   └── EditCellWindowViewModel.cs ← نافذة MIDI Learn
    └── Views/
        ├── Theme.xaml        ← التنسيقات والألوان
        └── EditCellWindow.xaml/.cs ← نافذة MIDI Learn
```

---

## ٧. أولويات الجلسة الحالية

التركيز الآن على **المرحلة الأولى فقط**:

1. ~~إصلاح `setInputMixMute` في الخادم~~
2. ~~إصلاح `setMixMute` في الخادم~~
3. ~~إصلاح `isMuted` في mixes~~
4. ~~إصلاح `isMuted` في cells~~

بعد ما نخلص، ننتقل للمرحلة الثانية.

---

> آخر تحديث: ٢٠٢٦-٠٧-١٥
