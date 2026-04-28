# 🚀 دليل رفع نظام CentralAuthNotificationPlatform على Railway

هذا الدليل يشرح الخطوات اللازمة لرفع النظام وضبط قاعدة البيانات ليعمل بشكل صحيح.

## 📋 المتطلبات الأساسية
1. حساب على **Railway**.
2. مستودع (Repository) على **GitHub** يحتوي على الكود.
3. قاعدة بيانات **SQL Server** (مثلاً من MonsterASP) مع تفعيل الوصول عن بعد (Remote Access).

---

## 🛠️ إعدادات Railway (Variables)

يجب إضافة المتغيرات التالية في تبويب **Variables** لكل خدمة:

| المتغير | القيمة / الوصف |
| :--- | :--- |
| `CONNECTION_STRING` | رابط الاتصال العام (Remote) بقاعدة البيانات. |
| `APPLY_MIGRATIONS` | اضبطه على `true` (لإنشاء الجداول تلقائياً عند أول تشغيل). |
| `JWT_SECRET` | سلسلة نصية عشوائية (لا تقل عن 32 حرفاً) لتأمين الـ Tokens. |
| `ASPNETCORE_ENVIRONMENT` | اضبطه على `Production`. |
| `ALLOWED_ORIGINS` | النطاقات المسموح لها بالاتصال (مثلاً: `*` أو رابط الـ Frontend). |
| `PORT` | اتركه فارغاً (Railway سيقوم بضبطه تلقائياً على 8080). |

> [!IMPORTANT]
> تأكد من استخدام رابط الـ **Remote Access** لقاعدة البيانات (dbXXXX.public.databaseasp.net) وليس الرابط المحلي.

---

## ⚙️ إعدادات الخدمة (Settings)

عند إضافة الخدمة من GitHub، تأكد من الضبط التالي في تبويب **Settings**:

1. **Root Directory:** `/Backend` (لأن ملف الـ Dockerfile موجود داخل هذا المجلد).
2. **Build Configuration:** سيقوم Railway باكتشاف الـ Dockerfile تلقائياً داخل مجلد Backend.

---

## 🔄 خطوات الرفع (Deployment Steps)

1. اذهب لـ [Railway Dashboard](https://railway.com/dashboard).
2. اضغط على **New Project** -> **Deploy from GitHub repo**.
3. اختر المستودع الخاص بالمشروع.
4. بعد إنشاء الخدمة، اذهب لـ **Settings** وغير الـ **Root Directory** إلى `/Backend`.
5. اذهب لـ **Variables** وأضف القيم المذكورة أعلاه.
6. سيبدأ Railway ببناء المشروع (Build) ثم تشغيله (Deploy) تلقائياً.

---

## 🩺 التأكد من التشغيل
بعد انتهاء الرفع، يمكنك التأكد من صحة العمل من خلال:
*   **Deploy Logs:** يجب أن ترى رسالة `EF Core migrations applied`.
*   **Health Check:** افتح رابط المشروع وأضف `/health` في النهاية (يجب أن يظهر `{"status": "Healthy"}`).

---
**تم إعداد هذا الدليل بواسطة ’* 🤖🔥
