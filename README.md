## Version Compatibility

| Plugin version | NopCommerce | .NET   | Branch                                                                    | Status      |
|---------------|-------------|--------|---------------------------------------------------------------------------|-------------|
| v3.x          | 4.90.x      | 9.0    | [main](../../tree/main)                                                   | Active dev  |
| v2.x          | 4.70.x      | 8.0    | [nop/4.7](../../tree/nop/4.7)                                             | Stable      |
| v1.x          | 4.30.x      | 3.1    | [nop/4.3](../../tree/nop/4.3)                                             | Stable      |

---

<div dir="rtl">

## جدول سازگاری نسخه‌ها

| نسخه افزونه | NopCommerce | .NET | شاخه | وضعیت |
|-------------|-------------|------|-------|--------|
| v3.x | 4.90.x | 9.0 | [main](../../tree/main) | توسعه فعال |
| v2.x | 4.70.x | 8.0 | [nop/4.7](../../tree/nop/4.7) | پایدار |
| v1.x | 4.30.x | 3.1 | [nop/4.3](../../tree/nop/4.3) | پایدار |

---

# افزونه پرداخت بانک سامان برای NopCommerce

افزونه‌ای برای اتصال NopCommerce به درگاه اینترنتی بانک سامان (SEP) با استفاده از API نسخه ۲ (REST/JSON).

## ویژگی‌ها

- اتصال به درگاه پرداخت اینترنتی بانک سامان (SEP v2)
- دریافت توکن پرداخت، هدایت خریدار به درگاه و بازگشت خودکار
- تأیید تراکنش (Verify) پس از بازگشت از درگاه
- پشتیبانی از چند فروشگاه (Multi-Store) با تنظیمات جداگانه
- تبدیل ارز: تبدیل خودکار از طریق نرخ ارز NopCommerce یا ضریب دستی (تومان / ریال)
- لاگ‌گیری کامل از هر مرحله برای شناسایی سریع خطا

## پیش‌نیازها

| مورد | نسخه |
|------|-------|
| NopCommerce | 4.3.x |
| .NET | Core 3.1 |
| شناسه پایانه (TerminalId) | فعال‌شده توسط بانک سامان |
| آدرس ReturnUrl | **باید از اینترنت عمومی در دسترس باشد** — localhost توسط بانک رد می‌شود |

## نصب

### روش ۱ — کامپایل از سورس

1. پروژه را در کنار سایر افزونه‌های NopCommerce قرار دهید:
   ```
   Nop4.3/Plugins/Nop.Plugin.Payments.Saman/
   ```
2. در Visual Studio یا از خط فرمان بیلد کنید:
   ```bash
   dotnet build --configuration Release
   ```
   فایل‌های خروجی به‌طور خودکار در مسیر زیر کپی می‌شوند:
   ```
   Presentation/Nop.Web/Plugins/Payments.Saman/
   ```
3. سایت را ری‌استارت کنید.
4. در پنل ادمین به مسیر زیر بروید:
   **Configuration → Local plugins → Find "Saman Bank (SEP)" → Install**

### روش ۲ — کپی مستقیم DLL

1. بیلد نهایی را از قسمت [Releases](../../releases) دانلود کنید.
2. محتوا را در پوشه زیر کپی کنید:
   ```
   Presentation/Nop.Web/Plugins/Payments.Saman/
   ```
3. سایت را ری‌استارت و افزونه را از پنل ادمین نصب کنید.

## پیکربندی

پس از نصب به مسیر زیر بروید:
**Configuration → Payment methods → Saman Bank (SEP) → Configure**

| فیلد | توضیح |
|------|--------|
| **Terminal ID** | شناسه پایانه دریافت‌شده از بانک سامان |
| **Amount Multiplier** | ضریب تبدیل مبلغ (ریال/تومان) — جدول زیر را ببینید |

### تنظیم مبلغ

| ارز فروشگاه | ضریب | توضیح |
|-------------|------|--------|
| ریال ایران (IRR) | `1` | NopCommerce مستقیم به ریال ارسال می‌شود |
| تومان | `10` | هر تومان = ۱۰ ریال |
| ارز دیگر + IRR تعریف‌شده | — | تبدیل خودکار از طریق نرخ ارز NopCommerce انجام می‌شود |

> **نکته:** حداقل مبلغ قابل قبول بانک سامان **۱,۰۰۰ ریال** است.

## نحوه کار (جریان پرداخت)

```
خریدار روی "پرداخت" کلیک می‌کند
        ↓
PostProcessPayment: درخواست توکن به API بانک
        ↓
PaymentForm: فرم مخفی — هدایت خودکار به درگاه بانک
        ↓
خریدار اطلاعات کارت را وارد و پرداخت می‌کند
        ↓
Return (GET/POST از بانک): دریافت Status, RefNum, ResNum
        ↓
VerifyTransaction: تأیید تراکنش با API بانک
        ↓
MarkOrderAsPaid: تغییر وضعیت سفارش + هدایت به صفحه تکمیل
```

## کدهای وضعیت بانک (Status)

| Status | معنا | اقدام افزونه |
|--------|------|--------------|
| `1` | پرداخت موفق | تأیید (Verify) و ثبت پرداخت |
| `2` | پرداخت تأیید‌شده (برخی حالت‌های پایانه) | تأیید (Verify) و ثبت پرداخت |
| `< 0` | لغو / خطا / اتمام زمان | رد — بازگشت به صفحه جزئیات سفارش |

## رفع مشکلات

همه رویدادهای افزونه با پیشوند `[Saman]` در جدول لاگ NopCommerce ذخیره می‌شوند:
**Admin → System → Log**

| مشکل | دلیل احتمالی |
|------|---------------|
| توکن دریافت نمی‌شود: «آدرس سرور نامعتبر» | ReturnUrl شما localhost است؛ از ngrok یا سرور واقعی استفاده کنید |
| توکن دریافت نمی‌شود: «مبلغ کمتر از ۱,۰۰۰ ریال» | Amount Multiplier را روی ۱۰ تنظیم کنید (برای تومان) |
| وضعیت سفارش تغییر نمی‌کند | لاگ `[Saman Return] Verify response` را بررسی کنید |
| خطای CSRF در بازگشت | مطمئن شوید Return action دارای `[IgnoreAntiforgeryToken]` است |

## ساختار پروژه

```
Nop.Plugin.Payments.Saman/
├── Controllers/
│   └── PaymentSamanController.cs   # Configure, PaymentForm, Return
├── Infrastructure/
│   ├── NopStartup.cs               # ثبت HttpClient
│   └── RouteProvider.cs            # مسیرهای افزونه
├── Models/
│   └── ConfigurationModel.cs
├── Services/
│   └── SamanHttpClient.cs          # ارتباط با API بانک
├── Views/
│   ├── Configure.cshtml
│   ├── PaymentForm.cshtml          # فرم هدایت خودکار به درگاه
│   └── PaymentInfo.cshtml
├── SamanPaymentDefaults.cs         # آدرس‌های API و ثابت‌ها
├── SamanPaymentProcessor.cs        # پیاده‌سازی IPaymentMethod
├── SamanPaymentSettings.cs
└── plugin.json
```

## مجوز

MIT License — آزاد برای استفاده در پروژه‌های تجاری و غیرتجاری.

</div>

---

# Saman Bank Payment Plugin for NopCommerce

A NopCommerce plugin integrating the Saman Bank (SEP) internet payment gateway using the SEP v2 REST/JSON API.

## Features

- Full SEP v2 REST API integration (token request → redirect → verify)
- Automatic customer redirect to the bank payment page and back
- Transaction verification via Saman's Verify API before marking orders as paid
- Multi-store support with per-store setting overrides
- Smart currency conversion: automatic via NopCommerce IRR exchange rate, or manual multiplier fallback (Toman/Rial)
- Comprehensive structured logging (all events prefixed `[Saman]`) for fast troubleshooting

## Requirements

| Item | Version / Note |
|------|---------------|
| NopCommerce | 4.3.x |
| .NET | Core 3.1 |
| Terminal ID | Issued by Saman Bank |
| Return URL | **Must be publicly reachable** — Saman Bank rejects `localhost` URLs |

## Installation

### Option 1 — Build from Source

1. Place the project alongside other NopCommerce plugins:
   ```
   Nop4.3/Plugins/Nop.Plugin.Payments.Saman/
   ```
2. Build (Visual Studio or CLI):
   ```bash
   dotnet build --configuration Release
   ```
   Output is automatically copied to:
   ```
   Presentation/Nop.Web/Plugins/Payments.Saman/
   ```
3. Restart the site.
4. In the admin panel go to:  
   **Configuration → Local plugins → Find "Saman Bank (SEP)" → Install**

### Option 2 — Pre-built DLL

1. Download the latest build from [Releases](../../releases).
2. Copy the contents to:
   ```
   Presentation/Nop.Web/Plugins/Payments.Saman/
   ```
3. Restart the site and install from the admin panel.

## Configuration

After installation navigate to:  
**Configuration → Payment methods → Saman Bank (SEP) → Configure**

| Field | Description |
|-------|-------------|
| **Terminal ID** | Your terminal ID provided by Saman Bank |
| **Amount Multiplier** | Amount conversion factor — see table below |

### Amount Multiplier Guide

| Store Currency | Multiplier | Notes |
|----------------|-----------|-------|
| Iranian Rial (IRR) | `1` | Amount sent as-is |
| Iranian Toman | `10` | 1 Toman = 10 Rials |
| Other currency + IRR defined | — | Auto-converted via NopCommerce exchange rate |

> **Minimum:** Saman Bank requires a minimum of **1,000 Rials** per transaction.

## Payment Flow

```
Customer clicks "Place Order"
        ↓
PostProcessPayment: request payment token from Saman API
        ↓
PaymentForm: hidden auto-submit form → customer redirected to Saman Bank
        ↓
Customer enters card details and pays on Saman's page
        ↓
Return (GET or POST from Saman): receives Status, RefNum, ResNum
        ↓
VerifyTransaction: calls Saman Verify API to confirm the transaction
        ↓
MarkOrderAsPaid: order status updated → customer redirected to order completion page
```

## Saman Bank Status Codes

| Status | Meaning | Plugin action |
|--------|---------|---------------|
| `1` | Successful payment | Proceed to Verify → mark order as paid |
| `2` | Authorized (some terminal modes) | Proceed to Verify → mark order as paid |
| `< 0` | Cancelled / error / timeout | Fail immediately → redirect to order details |

The `VerifyTransaction` API result (`ResultCode == 0`) is the **authoritative** success indicator. The `Status` field is only used to skip verification for definitive failures (negative codes).

## Troubleshooting

All plugin events are logged with the `[Saman]` prefix under:  
**Admin → System → Log**

| Symptom | Likely cause |
|---------|-------------|
| Token request fails: "invalid server address" | Return URL is `localhost`; use ngrok or a real server |
| Token request fails: "amount less than 1,000 Rials" | Set Amount Multiplier to `10` (for Toman stores) |
| Order status not updated after payment | Check `[Saman Return] Verify response` log entry |
| CSRF error on return | Ensure the `Return` action has `[IgnoreAntiforgeryToken]` |

## Project Structure

```
Nop.Plugin.Payments.Saman/
├── Controllers/
│   └── PaymentSamanController.cs   # Configure, PaymentForm, Return actions
├── Infrastructure/
│   ├── NopStartup.cs               # HttpClient registration
│   └── RouteProvider.cs            # Plugin route definitions
├── Models/
│   └── ConfigurationModel.cs
├── Services/
│   └── SamanHttpClient.cs          # Saman Bank API client
├── Views/
│   ├── Configure.cshtml
│   ├── PaymentForm.cshtml          # Auto-submit redirect form
│   └── PaymentInfo.cshtml
├── SamanPaymentDefaults.cs         # API URLs and constants
├── SamanPaymentProcessor.cs        # IPaymentMethod implementation
├── SamanPaymentSettings.cs
└── plugin.json
```

## API Endpoints (SEP v2)

| Purpose | URL |
|---------|-----|
| Token request | `https://sep.shaparak.ir/onlinepg/onlinepg` |
| Payment page | `https://sep.shaparak.ir/OnlinePG/OnlinePG` |
| Verify transaction | `https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/VerifyTransaction` |

## License

MIT License — free to use in commercial and non-commercial projects.
