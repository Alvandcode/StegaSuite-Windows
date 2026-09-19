using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StegaSuite.Core;

namespace StegaSuite;

public partial class MainWindow : Window
{
    private static readonly string[] Langs = { "fa", "ar", "en", "ru", "zh" };
    private int _langIdx;
    private string _lang => Langs[_langIdx];
    private int _themeIdx;
    private int _page;
    private string? _user;
    private bool _authLoginMode = true;
    private bool _authRestoreMode;
    private string? _pendingCode;
    private string? _restoreFile;
    private readonly Dictionary<string, (int n, DateTime until)> _loginFails = new();
    private DispatcherTimer? _idleTimer;
    private bool _updateChecked;
    private const string AppVersion = "2.0.0";

    internal static readonly string[] ThemeFiles =
    {
        "Purple.xaml", "CyberBlue.xaml", "Crimson.xaml",
        "Rose.xaml", "Mint.xaml", "Lavender.xaml",
        "Graphite.xaml", "Porcelain.xaml", "Cream.xaml",
    };
    // Theme display names in [fa, ar, en, ru, zh] — same order as Langs.
    internal static readonly string[][] ThemeNames =
    {
        new[] { "بنفش", "بنفسجي", "Purple", "Фиолетовый", "紫色" },
        new[] { "آبی سایبری", "أزرق سايبر", "Cyber Blue", "Кибер-синий", "赛博蓝" },
        new[] { "زرشکی", "قرمزي عميق", "Crimson", "Малиновый", "深红色" },
        new[] { "صورتی", "وردي", "Rose", "Розовый", "粉色" },
        new[] { "نعنایی", "نعناعي", "Mint", "Мятный", "薄荷绿" },
        new[] { "یاسی", "خزامي", "Lavender", "Лавандовый", "薰衣草紫" },
        new[] { "خاکستری تیره", "رمادي داكن", "Dark Gray", "Тёмно-серый", "深灰色" },
        new[] { "سفید زغال‌سنگی", "أبيض فحمي", "Charcoal White", "Угольно-белый", "炭白色" },
        new[] { "کرمی روشن", "كريمي فاتح", "Light Cream", "Светло-кремовый", "浅奶油色" },
    };
    internal string ThemeLabel(int i) => ThemeNames[i][_langIdx];

    private const string GithubUrl = "https://github.com/Alvandcode/StegaSuite";
    private const string Wallet = "UQCB9rzvwmq0FJDaBkHVdBgbfZPb06FWdKco3woAHH6AXuUt";

    private readonly Dictionary<string, string[]> _tr = new()
    {
        ["title"] = new[] { "✦ استگانوسویت", "✦ ستيغاسويت", "✦ StegaSuite", "✦ StegaSuite", "✦ StegaSuite" },
        ["subtitle"] = new[] { "مخفی‌سازی امن فایل‌ها • نسخه ویندوز", "إخفاء الملفات بأمان • نسخة ويندوز", "Secure file steganography • Windows edition", "Безопасная стеганография • Windows", "安全文件隐写 • Windows 版" },
        ["home"] = new[] { "خانه", "الرئيسية", "Home", "Главная", "首页" },
        ["extract"] = new[] { "استخراج", "استخراج", "Extract", "Извлечь", "提取" },
        ["about"] = new[] { "درباره برنامه", "حول التطبيق", "About", "О программе", "关于" },
        ["contact"] = new[] { "تماس با ما", "اتصل بنا", "Contact", "Контакты", "联系" },
        ["support"] = new[] { "حمایت", "الدعم", "Support", "Поддержка", "支持" },
        ["settings"] = new[] { "تنظیمات", "الإعدادات", "Settings", "Настройки", "设置" },
        ["carrier"] = new[] { "فایل حامل", "الملف الحامل", "Carrier file", "Файл-носитель", "载体文件" },
        ["payload"] = new[] { "فایل مخفی‌شونده", "الملف المراد إخفاؤه", "File to hide", "Скрываемый файл", "要隐藏的文件" },
        ["password"] = new[] { "رمز عبور (اختیاری)", "كلمة المرور (اختياري)", "Password (optional)", "Пароль (необязательно)", "密码（可选）" },
        ["show"] = new[] { "نمایش", "إظهار", "Show", "Показать", "显示" },
        ["browse"] = new[] { "انتخاب فایل...", "اختيار ملف...", "Browse...", "Выбрать файл...", "选择文件..." },
        ["hideBtn"] = new[] { "مخفی کن و ذخیره کن", "إخفاء وحفظ", "Hide & Save", "Скрыть и сохранить", "隐藏并保存" },
        ["extractBtn"] = new[] { "استخراج فایل مخفی", "استخراج الملف المخفي", "Extract Hidden File", "Извлечь скрытый файл", "提取隐藏文件" },
        ["ready"] = new[] { "آماده", "جاهز", "Ready", "Готово", "就绪" },
        ["working"] = new[] { "در حال پردازش...", "جارٍ المعالجة...", "Working...", "Обработка...", "处理中..." },
        ["status"] = new[] { "وضعیت", "الحالة", "Status", "Статус", "状态" },
        ["capacity"] = new[] { "ظرفیت", "السعة", "Capacity", "Ёмкость", "容量" },
        ["type"] = new[] { "نوع", "النوع", "Type", "Тип", "类型" },
        ["doneHide"] = new[] { "ذخیره شد: ", "تم الحفظ: ", "Saved: ", "Сохранено: ", "已保存：" },
        ["doneExtract"] = new[] { "استخراج شد: ", "تم الاستخراج: ", "Extracted: ", "Извлечено: ", "已提取：" },
        ["error"] = new[] { "خطا: ", "خطأ: ", "Error: ", "Ошибка: ", "错误：" },
        ["needBoth"] = new[] { "اول فایل حامل و فایل مخفی را انتخاب کنید.", "اختر ملف الحامل والملف المخفي أولاً.", "Select carrier and payload files first.", "Сначала выберите носитель и скрываемый файл.", "请先选择载体文件和要隐藏的文件。" },
        ["needCarrier"] = new[] { "اول فایل حامل را انتخاب کنید.", "اختر ملف الحامل أولاً.", "Select a carrier file first.", "Сначала выберите файл-носитель.", "请先选择载体文件。" },
        ["hintFormats"] = new[] { "هر فرمتی: سند، عکس، صدا، ZIP...", "أي تنسيق: مستندات، صور، صوت، ZIP...", "Any format: docs, images, audio, ZIP...", "Любой формат: документы, фото, аудио, ZIP...", "任何格式：文档、图片、音频、ZIP..." },
        ["language"] = new[] { "زبان", "اللغة", "Language", "Язык", "语言" },
        ["darkGroup"] = new[] { "تیره • سایبرپانک", "داكنة • سايبربانك", "Dark • Cyberpunk", "Тёмные • Киберпанк", "深色 • 赛博朋克" },
        ["lightGroup"] = new[] { "روشن • پاستلی", "فاتحة • باستيل", "Light • Pastel", "Светлые • Пастель", "浅色 • 马卡龙" },
        ["aboutTitle"] = new[] { "درباره استگانوسویت", "حول ستيغاسويت", "About StegaSuite", "О StegaSuite", "关于 StegaSuite" },
        ["aboutDesc"] = new[] { "استگانوسویت فایل‌های شما را با رمزنگاری قوی، داخل فایل‌های به‌ظاهر عادی پنهان می‌کند؛ طوری که حتی وجود آن‌ها هم مخفی می‌ماند.", "يخفي ستيغاسويت ملفاتك بتشفير قوي داخل ملفات عادية، بحيث يبقى وجودها مخفيًا أيضًا.", "StegaSuite hides your files with strong encryption inside ordinary-looking files, so even their existence stays hidden.", "StegaSuite скрывает ваши файлы с надёжным шифрованием внутри обычных файлов — скрытым остаётся даже сам факт.", "StegaSuite 用强加密将您的文件隐藏在普通文件中，甚至连文件的存在也不会被发现。" },
        ["features"] = new[] { "✓ رمزنگاری AES-256-GCM با PBKDF2\n✓ پنهان‌سازی در عکس، صدا و فایل‌های دیگر\n✓ بازگشت فایل با نام اصلی\n✓ کاملاً آفلاین، بدون اینترنت", "✓ تشفير AES-256-GCM مع PBKDF2\n✓ الإخفاء في الصور والصوت والملفات\n✓ استعادة الملف بالاسم الأصلي\n✓ يعمل دون إنترنت تمامًا", "✓ AES-256-GCM encryption with PBKDF2\n✓ Hide in images, audio and files\n✓ Original filename restored\n✓ Fully offline", "✓ Шифрование AES-256-GCM с PBKDF2\n✓ Скрытие в изображениях, аудио и файлах\n✓ Восстановление исходного имени\n✓ Полностью офлайн", "✓ AES-256-GCM 加密 + PBKDF2\n✓ 隐藏于图片、音频和文件中\n✓ 恢复原始文件名\n✓ 完全离线" },
        ["tutorial"] = new[] { "📖 آموزش کامل", "📖 الدليل الكامل", "📖 Full tutorial", "📖 Руководство", "📖 完整教程" },
        ["contactTitle"] = new[] { "تماس با ما", "اتصل بنا", "Contact Us", "Контакты", "联系我们" },
        ["supportTitle"] = new[] { "حمایت از پروژه", "ادعم المشروع", "Support the Project", "Поддержка проекта", "支持项目" },
        ["supportStar"] = new[] { "اگر از برنامه راضی هستید، در گیت‌هاب به آن ستاره بدهید!", "إذا أعجبك التطبيق، امنحه نجمة على GitHub!", "If you like the app, give it a star on GitHub!", "Нравится приложение? Поставьте звезду на GitHub!", "如果喜欢这个应用，请在 GitHub 上给它加星！" },
        ["supportStarBtn"] = new[] { "⭐ ستاره در GitHub", "⭐ نجمة على GitHub", "⭐ Star on GitHub", "⭐ Звезда на GitHub", "⭐ 在 GitHub 加星" },
        ["supportWallet"] = new[] { "کیف پول TON:", "محفظة TON:", "TON wallet:", "Кошелёк TON:", "TON 钱包：" },
        ["supportCopy"] = new[] { "کپی آدرس", "نسخ العنوان", "Copy Address", "Копировать", "复制地址" },
        ["copied"] = new[] { "کپی شد ✓", "تم النسخ ✓", "Copied ✓", "Скопировано ✓", "已复制 ✓" },
        ["account"] = new[] { "حساب", "الحساب", "Account", "Аккаунт", "账户" },
        ["login"] = new[] { "ورود", "تسجيل الدخول", "Log in", "Войти", "登录" },
        ["register"] = new[] { "ثبت‌نام", "إنشاء حساب", "Sign up", "Регистрация", "注册" },
        ["logout"] = new[] { "خروج از حساب", "تسجيل الخروج", "Log out", "Выйти", "退出登录" },
        ["username"] = new[] { "نام کاربری", "اسم المستخدم", "Username", "Имя пользователя", "用户名" },
        ["password"] = new[] { "رمز عبور", "كلمة المرور", "Password", "Пароль", "密码" },
        ["confirmPass"] = new[] { "تکرار رمز عبور", "تأكيد كلمة المرور", "Confirm password", "Подтвердите пароль", "确认密码" },
        ["loginGo"] = new[] { "ورود به برنامه", "دخول إلى البرنامج", "Enter the app", "Войти в приложение", "进入应用" },
        ["registerGo"] = new[] { "ساخت حساب", "إنشاء الحساب", "Create account", "Создать аккаунт", "创建账户" },
        ["noAccount"] = new[] { "حساب ندارید؟ ثبت‌نام", "ليس لديك حساب؟ سجّل", "New here? Sign up", "Нет аккаунта? Создайте", "没有账户？注册" },
        ["haveAccount"] = new[] { "حساب دارید؟ ورود", "لديك حساب؟ ادخل", "Have an account? Log in", "Есть аккаунт? Войдите", "已有账户？登录" },
        ["forgotPass"] = new[] { "فراموشی رمز (بازیابی با بکاپ)", "نسيت كلمة المرور؟ (استعادة)", "Forgot password? (restore)", "Забыли пароль? (копия)", "忘记密码？（备份恢复）" },
        ["backToLogin"] = new[] { "بازگشت به ورود", "عودة إلى الدخول", "Back to log in", "Назад ко входу", "返回登录" },
        ["authFillAll"] = new[] { "همه فیلدها را پر کنید.", "املأ كل الحقول.", "Fill in all fields.", "Заполните все поля.", "请填写所有字段。" },
        ["userExists"] = new[] { "این نام کاربری قبلاً ثبت شده است.", "هذا الاسم مسجّل مسبقًا.", "Username is already taken.", "Имя уже занято.", "用户名已被占用。" },
        ["userNotFound"] = new[] { "کاربری با این نام پیدا نشد.", "لا يوجد مستخدم بهذا الاسم.", "User not found.", "Пользователь не найден.", "未找到该用户。" },
        ["wrongPass"] = new[] { "رمز عبور اشتباه است.", "كلمة المرور خاطئة.", "Wrong password.", "Неверный пароль.", "密码错误。" },
        ["passMismatch"] = new[] { "تکرار رمز با رمز یکی نیست.", "التأكيد غير مطابق.", "Passwords do not match.", "Пароли не совпадают.", "两次输入的密码不一致。" },
        ["passShort"] = new[] { "رمز عبور حداقل ۴ نویسه باشد.", "٤ أحرف على الأقل.", "At least 4 characters.", "Минимум 4 символа.", "密码至少 4 个字符。" },
        ["welcome"] = new[] { "خوش آمدید! ", "مرحبًا! ", "Welcome! ", "Добро пожаловать! ", "欢迎！" },
        ["codeCap"] = new[] { "کد بازیابی شما — فقط یک‌بار نمایش داده می‌شود؛ جای امن نگه دارید:", "رمز الاسترداد — يظهر مرة واحدة فقط؛ احفظه في مكان آمن:", "Your recovery code — shown only once; keep it somewhere safe:", "Код восстановления — показан только один раз; сохраните его:", "您的恢复码——仅显示一次，请妥善保管：" },
        ["continueBtn"] = new[] { "ادامه", "متابعة", "Continue", "Продолжить", "继续" },
        ["promoTitle1"] = new[] { "تازه‌واردی؟", "جديد هنا؟", "New here?", "Вы новенький?", "新用户？" },
        ["promoDesc1"] = new[] { "حساب بساز و وارد دنیای امن فایل‌ها شو.", "أنشئ حسابًا وادخل عالم الملفات الآمنة.", "Create an account and step into secure files.", "Создайте аккаунт и войдите в мир безопасных файлов.", "创建账户，进入安全文件的世界。" },
        ["promoTitle2"] = new[] { "برگشتی؟", "عدت إلينا؟", "Welcome back!", "С возвращением!", "欢迎回来！" },
        ["promoDesc2"] = new[] { "خوش برگشتی! وارد شو و ادامه بده.", "مرحبًا بعودتك! ادخل وتابع.", "Good to see you again — log in and continue.", "Рады видеть снова — войдите и продолжите.", "很高兴再次见到你，请登录继续。" },
        ["orContinue"] = new[] { "یا", "أو", "or", "или", "或" },
        ["copyCode"] = new[] { "کپی کد", "نسخ الرمز", "Copy code", "Копировать код", "复制恢复码" },
        ["firstBackupNotice"] = new[] { "از بخش «تنظیمات»، با کد بازیابی «فایل بکاپ حساب» را دریافت کنید تا اگر رمزتان را فراموش کردید، امکان بازیابی حساب وجود داشته باشد.", "من «الإعدادات» احصل على «ملف النسخة» باستخدام رمز الاسترداد، لتتمكن من استعادة حسابك عند نسيان كلمة المرور.", "From “Settings”, use your recovery code to export your “account backup file”, so you can recover your account if you forget your password.", "В «Настройках» сохраните «файл копии» с помощью кода восстановления, чтобы вернуть доступ при потере пароля.", "请到“设置”中使用恢复码导出“账户备份文件”，以便忘记密码时恢复账户。" },
        ["loggedInAs"] = new[] { "واردشده به‌عنوان: ", "مسجّل باسم: ", "Signed in as: ", "Вы вошли как: ", "当前用户：" },
        ["exportBackup"] = new[] { "دریافت فایل بکاپ", "تنزيل النسخة", "Export backup file", "Скачать файл копии", "导出备份文件" },
        ["backupDone"] = new[] { "فایل بکاپ ذخیره شد: ", "تم حفظ النسخة: ", "Backup saved: ", "Копия сохранена: ", "备份已保存：" },
        ["newCodeBtn"] = new[] { "کد بازیابی جدید", "رمز استرداد جديد", "New recovery code", "Новый код", "新的恢复码" },
        ["pickBackup"] = new[] { "انتخاب فایل بکاپ...", "اختيار ملف النسخة...", "Choose backup file...", "Выбрать файл копии...", "选择备份文件..." },
        ["recoveryCode"] = new[] { "کد بازیابی", "رمز الاسترداد", "Recovery code", "Код восстановления", "恢复码" },
        ["newPass"] = new[] { "رمز عبور جدید", "كلمة مرور جديدة", "New password", "Новый пароль", "新密码" },
        ["restoreGo"] = new[] { "بازیابی و ورود", "استعادة ودخول", "Restore & log in", "Восстановить и войти", "恢复并登录" },
        ["restoreTitle"] = new[] { "بازیابی حساب با فایل بکاپ", "استعادة الحساب من نسخة", "Restore account from backup", "Восстановление из копии", "从备份恢复账户" },
        ["restoreDone"] = new[] { "حساب بازیابی شد؛ وارد شدید!", "تمت الاستعادة؛ دخلت!", "Account restored — logged in!", "Аккаунт восстановлен — вход выполнен!", "账户已恢复，登录成功！" },
        ["restoreFail"] = new[] { "بازیابی ناموفق بود (فایل یا کد اشتباه است).", "فشلت الاستعادة (ملف أو رمز خاطئ).", "Restore failed (wrong file or code).", "Ошибка восстановления (файл или код неверны).", "恢复失败（文件或恢复码错误）。" },
        ["backupHint"] = new[] { "فایل بکاپ + کد بازیابی یعنی اگر رمزتان را فراموش کنید، حسابتان قابل بازیابی است. هر دو را امن نگه دارید.", "النسخة + رمز الاسترداد = استعادة حسابك عند نسيان كلمة المرور. احفظهما بأمان.", "Backup file + recovery code = your account can be recovered if you forget your password. Keep both safe.", "Файл копии + код = восстановление при потере пароля. Храните их в безопасности.", "备份文件 + 恢复码 = 忘记密码时可恢复账户。请妥善保管两者。" },
        ["needGmail"] = new[] { "آدرس جیمیل را در فیلد نام کاربری وارد کنید.", "أدخل بريد Gmail في حقل الاسم.", "Enter your Gmail address in the username field.", "Введите Gmail в поле имени.", "请在用户名栏输入 Gmail 地址。" },
        ["needClientId"] = new[] { "ورود گوگل فعال نیست — با دکمه راهنما Client ID بسازید.", "دخول Google غير مفعّل — أنشئ Client ID بزر المساعدة.", "Google sign-in isn't set up — create a Client ID via the guide button.", "Вход через Google не настроен — создайте Client ID по кнопке-инструкции.", "尚未设置 Google 登录——请按指南按钮创建客户端 ID。" },
        ["badClientId"] = new[] { "این Client ID معتبر نیست؛ باید با apps.googleusercontent.com تمام شود.", "معرّف العميل غير صالح؛ يجب أن ينتهي بـ apps.googleusercontent.com.", "This client ID is invalid; it must end with apps.googleusercontent.com.", "Недействительный ID клиента; он должен заканчиваться на apps.googleusercontent.com.", "客户端 ID 无效；必须以 apps.googleusercontent.com 结尾。" },
        ["errClientType"] = new[] { "نوع کلاینت باید Desktop app باشد؛ به‌نظر می‌رسد نوع Web ساخته‌اید. در کنسول گوگل یک Client جدید از نوع Desktop بسازید.", "يجب أن يكون النوع Desktop app؛ يبدو أنك أنشأت Web. أنشئ عميلًا جديدًا من نوع Desktop.", "The client type must be “Desktop app” — yours looks like a Web client. Create a new Desktop-type client.", "Тип должен быть «Desktop app» — похоже, создан Web-клиент. Создайте новый клиент типа Desktop.", "客户端类型必须为“桌面应用”——您似乎创建了 Web 类型。请新建桌面应用类型客户端。" },
        ["errRedirect"] = new[] { "آدرس بازگشت پذیرفته نشد؛ نوع کلاینت باید Desktop باشد.", "لم يُقبل عنوان العودة؛ يجب أن يكون النوع Desktop.", "Redirect URI not accepted; the client type must be Desktop.", "URI перенаправления не принят; тип должен быть Desktop.", "重定向 URI 未被接受；客户端类型必须为桌面应用。" },
        ["errDenied"] = new[] { "دسترسی در صفحه گوگل لغو شد؛ دوباره تلاش کنید.", "تم إلغاء الوصول في صفحة Google؛ حاول مجددًا.", "Access was denied on the Google page; try again.", "Доступ отклонён на странице Google; попробуйте снова.", "在 Google 页面上拒绝了访问；请重试。" },
        ["errNetwork"] = new[] { "ارتباط با سرور توکن گوگل برقرار نشد. اینترنت، VPN/پروکسی یا فایروال را بررسی کنید؛ گاهی صفحه ورود گوگل باز می‌شود ولی سرور توکن جواب نمی‌دهد.", "تعذّر الاتصال بخادم رمز Google. تحقق من الإنترنت أو VPN/الوكيل أو الجدار الناري.", "Could not reach Google's token server. Check internet, VPN/proxy or firewall; sometimes the login page opens but the token server never answers.", "Не удалось соединиться с сервером токенов Google. Проверьте интернет, VPN/прокси или брандмауэр.", "无法连接到 Google 令牌服务器，请检查网络、VPN/代理或防火墙设置。" },
        ["gStep_browser"] = new[] { "مرورگر باز شد؛ ورود گوگل را کامل کنید…", "فُتح المتصفح؛ أكمل الدخول…", "Browser opened — complete Google sign-in…", "Браузер открыт — завершите вход…", "浏览器已打开，请完成 Google 登录…" },
        ["gStep_code"] = new[] { "کد دریافت شد؛ در حال گرفتن توکن…", "وصل الرمز؛ جارٍ الحصول على الرمز المميز…", "Code received; exchanging for tokens…", "Код получен; обмен на токены…", "已收到授权码，正在换取令牌…" },
        ["gStep_token"] = new[] { "توکن گرفته شد؛ در حال خواندن ایمیل…", "تم الحصول على الرمز؛ جارٍ قراءة البريد…", "Tokens received; reading profile…", "Токены получены; чтение профиля…", "已获取令牌，正在读取个人资料…" },
        ["googleSetupBtn"] = new[] { "راهنمای ساخت Client ID", "دليل إنشاء Client ID", "Client ID setup guide", "Как создать Client ID", "客户端 ID 设置指南" },
        ["googleSetup"] = new[] { "۱) وارد console.cloud.google.com شوید و یک Project بسازید.\n۲) بروید به APIs & Services > Credentials.\n۳) بزنید Create Credentials > OAuth client ID و نوع Desktop را انتخاب کنید.\n۴) همان Client ID را کپی کنید و در برنامه، تنظیمات > حساب ذخیره کنید.\nحالا صفحه کنسول گوگل باز می‌شود.", "١) ادخل إلى console.cloud.google.com وأنشئ مشروعًا.\n٢) انتقل إلى APIs & Services > Credentials.\n٣) اختر Create Credentials > OAuth client ID من نوع Desktop.\n٤) انسخ Client ID واحفظه في الإعدادات > الحساب.\nستُفتح صفحة Google الآن.", "1) Open console.cloud.google.com and create a project.\n2) Go to APIs & Services > Credentials.\n3) Click Create Credentials > OAuth client ID, type Desktop.\n4) Copy the Client ID and save it in the app under Settings > Account.\nThe Google console page will open now.", "1) Откройте console.cloud.google.com и создайте проект.\n2) Перейдите в APIs & Services > Credentials.\n3) Нажмите Create Credentials > OAuth client ID, тип Desktop.\n4) Скопируйте Client ID и сохраните в приложении: Настройки > Аккаунт.\nСейчас откроется консоль Google.", "1）打开 console.cloud.google.com 并创建项目。\n2）进入 APIs & Services > Credentials。\n3）点击 Create Credentials > OAuth client ID，选择桌面应用类型。\n4）复制客户端 ID 并保存在应用的“设置 > 账户”中。\nGoogle 控制台页面即将打开。" },
        ["usePassword"] = new[] { "این حساب با رمز عبور ساخته شده؛ رمز را وارد کنید.", "هذا الحساب بكلمة مرور؛ أدخلها.", "This account uses a password — enter it.", "Этот аккаунт с паролем — введите его.", "该账户使用密码登录，请输入密码。" },
        ["googleClient"] = new[] { "شناسه کلاینت گوگل (نوع Desktop)", "معرّف عميل Google (سطح المكتب)", "Google client ID (Desktop type)", "ID клиента Google (тип Desktop)", "Google 客户端 ID（桌面应用类型）" },
        ["googleSecret"] = new[] { "کلاینت سکرت گوگل (فقط اگر خطای secret داد)", "سرّ عميل Google (عند طلب الخطأ فقط)", "Google client secret (only if a secret error appears)", "Секрет клиента Google (только при ошибке)", "Google 客户端密钥（仅在报错时填写）" },
        ["googleSave"] = new[] { "ذخیره", "حفظ", "Save", "Сохранить", "保存" },
        ["saved"] = new[] { "ذخیره شد ✓", "تم الحفظ ✓", "Saved ✓", "Сохранено ✓", "已保存 ✓" },
        ["googleHint"] = new[] { "از Google Cloud یک OAuth Client از نوع Desktop بسازید و Client ID را اینجا ذخیره کنید تا ورود واقعی گوگل فعال شود.", "أنشئ عميل OAuth من نوع Desktop في Google Cloud واحفظ Client ID هنا لتفعيل دخول Google.", "Create a Desktop-type OAuth client in Google Cloud and save its client ID here to enable real Google sign-in.", "Создайте OAuth-клиент типа Desktop в Google Cloud и сохраните Client ID здесь.", "请在 Google Cloud 创建桌面应用类型的 OAuth 客户端，并在此保存客户端 ID。" },
        ["googleError"] = new[] { "ورود با گوگل ناموفق بود: ", "فشل الدخول عبر Google: ", "Google sign-in failed: ", "Ошибка входа через Google: ", "Google 登录失败：" },
        ["googleOk"] = new[] { "ورود موفق شد؛ این تب را ببندید و به برنامه برگردید.", "تم الدخول؛ أغلق التب وارجع.", "Signed in — close this tab and return to the app.", "Вход выполнен — закройте вкладку и вернитесь.", "登录成功，请关闭此标签页并返回应用。" },
        ["linkGoogleQ"] = new[] { "حسابی با این جیمیل و رمز عبور وجود دارد. ورود گوگل به آن لینک شود؟", "يوجد حساب بهذا البريد وكلمة مرور. ربط دخول Google به؟", "An account with this Gmail already uses a password. Link Google sign-in to it?", "Аккаунт с этим Gmail уже использует пароль. Привязать вход через Google?", "该 Gmail 已有密码账户。是否绑定 Google 登录？" },
        ["needSpace"] = new[] { "فایل مخفی برای این حامل خیلی بزرگ است.", "الملف المخفي كبير جدًا على هذا الحامل.", "The secret file is too big for this carrier.", "Скрываемый файл слишком велик для носителя.", "隐藏文件对于此载体来说太大了。" },
        ["history"] = new[] { "تاریخچه عملیات", "سجل العمليات", "Operation history", "История операций", "操作历史" },
        ["clearHistory"] = new[] { "پاک کردن تاریخچه", "مسح السجل", "Clear history", "Очистить историю", "清除历史" },
        ["emptyHistory"] = new[] { "هنوز عملیاتی ثبت نشده است.", "لا توجد عمليات بعد.", "No operations yet.", "Операций пока нет.", "暂无操作记录。" },
        ["autoLocked"] = new[] { "به‌علت عدم فعالیت قفل شد.", "تم القفل لعدم النشاط.", "Locked due to inactivity.", "Заблокировано из-за неактивности.", "因长时间无操作已锁定。" },
        ["lockedOut"] = new[] { "تلاش ناموفق زیاد است؛ {0} ثانیه دیگر تلاش کنید.", "محاولات فاشلة كثيرة؛ حاول بعد {0} ثانية.", "Too many failed attempts; try again in {0} seconds.", "Слишком много попыток; повторите через {0} с.", "失败尝试过多，请 {0} 秒后再试。" },
        ["updateTitle"] = new[] { "به‌روزرسانی جدید", "تحديث جديد", "Update available", "Доступно обновление", "有可用更新" },
        ["updateMsg"] = new[] { "نسخه جدید منتشر شده است. صفحه دانلود باز شود؟", "تم إصدار نسخة جديدة. فتح صفحة التنزيل؟", "A new version is available. Open the download page?", "Доступна новая версия. Открыть страницу загрузки?", "有新版本可用，是否打开下载页面？" },
    };

    private string T(string key) =>
        _tr.TryGetValue(key, out var arr) && _langIdx < arr.Length ? arr[_langIdx] : key;

    private readonly Button[] _navs = new Button[6];
    private readonly Button[] _langs = new Button[5];
    private readonly Button[] _themes = new Button[9];
    private readonly Grid[] _pages = new Grid[6];

    public MainWindow()
    {
        InitializeComponent();
        AppConfig.Load();
        _navs[0] = Nav0; _navs[1] = Nav1; _navs[2] = Nav2; _navs[3] = Nav3; _navs[4] = Nav4; _navs[5] = Nav5;
        _langs[0] = Lang0; _langs[1] = Lang1; _langs[2] = Lang2; _langs[3] = Lang3; _langs[4] = Lang4;
        _themes[0] = Theme0; _themes[1] = Theme1; _themes[2] = Theme2;
        _themes[3] = Theme3; _themes[4] = Theme4; _themes[5] = Theme5;
        _themes[6] = Theme6; _themes[7] = Theme7; _themes[8] = Theme8;
        _pages[0] = Page0; _pages[1] = Page1; _pages[2] = Page2;
        _pages[3] = Page3; _pages[4] = Page4; _pages[5] = Page5;
        RestoreStartupState();
        FWallet.Text = Wallet;
        PaintThemeSwatches();
        ApplyTheme();
        ApplyLang();
        SetPage(0);
        InitAuth();
        try
        {
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _idleTimer.Tick += IdleTick;
            _idleTimer.Start();
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
        catch { }
        CheckUpdates();
    }

    internal string Tr(string key) => T(key);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    private static TimeSpan IdleTime()
    {
        try
        {
            var i = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref i)) return TimeSpan.Zero;
            uint tick = (uint)Environment.TickCount;
            return TimeSpan.FromMilliseconds(tick >= i.dwTime ? tick - i.dwTime : 0);
        }
        catch { return TimeSpan.Zero; }
    }

    private void IdleTick(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_user) || _user == "__selftest") return;
            if (IdleTime() >= TimeSpan.FromMinutes(5)) DoLogout("autoLocked");
        }
        catch { }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        try
        {
            if (e.Reason == SessionSwitchReason.SessionLock
                && !string.IsNullOrEmpty(_user) && _user != "__selftest")
                Dispatcher.Invoke(() => DoLogout("autoLocked"));
        }
        catch { }
    }

    // Effective Google credentials: silent config.json override wins (keeps existing
    // setups working), otherwise the developer credentials baked into GoogleSecrets.
    // End users never see or touch these.
    private static string GoogleId() =>
        AppConfig.GoogleClientId.Trim().Length > 0 ? AppConfig.GoogleClientId.Trim() : GoogleSecrets.ClientId;
    private static string GoogleSecret()
    {
        string s = AppConfig.GoogleClientSecret.Trim();
        return s.Length > 0 ? s : GoogleSecrets.ClientSecret;
    }

    private void SaveUserPrefs()
    {
        try
        {
            if (!string.IsNullOrEmpty(_user) && _user != "__selftest")
                AuthStore.SavePrefs(_user, _langIdx, _themeIdx);
        }
        catch { }
    }

    private void SaveGlobalPrefs()
    {
        try
        {
            if (!AppConfig.PersistenceEnabled) return;
            AppConfig.Lang = _langIdx;
            AppConfig.Theme = _themeIdx;
            AppConfig.Save();
        }
        catch { }
    }

    /// <summary>Restores last session: language, theme and window geometry.</summary>
    private void RestoreStartupState()
    {
        try
        {
            _langIdx = Math.Max(0, Math.Min(4, AppConfig.Lang));
            _themeIdx = Math.Max(0, Math.Min(8, AppConfig.Theme));
            if (AppConfig.WinW >= 740 && AppConfig.WinH >= 620)
            {
                double vl = SystemParameters.VirtualScreenLeft;
                double vt = SystemParameters.VirtualScreenTop;
                double vw = SystemParameters.VirtualScreenWidth;
                double vh = SystemParameters.VirtualScreenHeight;
                double w = Math.Min(AppConfig.WinW, vw);
                double h = Math.Min(AppConfig.WinH, vh);
                double l = Math.Min(Math.Max(AppConfig.WinLeft, vl - w + 100), vl + vw - 100);
                double t = Math.Min(Math.Max(AppConfig.WinTop, vt), vt + vh - 60);
                Width = w; Height = h; Left = l; Top = t;
                if (AppConfig.WinMax) WindowState = WindowState.Maximized;
            }
        }
        catch { }
    }

    private void SaveGeometry()
    {
        try
        {
            if (!AppConfig.PersistenceEnabled) return;
            AppConfig.WinMax = WindowState == WindowState.Maximized;
            Rect b = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            AppConfig.WinW = b.Width; AppConfig.WinH = b.Height;
            AppConfig.WinLeft = b.Left; AppConfig.WinTop = b.Top;
            AppConfig.Save();
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        try { SaveGeometry(); } catch { }
        base.OnClosed(e);
    }

    // ── Internal API for SelfTest ──
    internal string Lang => _lang;
    internal int ThemeIdx => _themeIdx;
    internal int Page => _page;
    internal void SetLang(int i) { _langIdx = ((i % Langs.Length) + Langs.Length) % Langs.Length; ApplyLang(); SaveUserPrefs(); SaveGlobalPrefs(); }
    internal void SwitchLanguage() => SetLang(_langIdx + 1);
    internal void SetTheme(int i) { _themeIdx = ((i % ThemeFiles.Length) + ThemeFiles.Length) % ThemeFiles.Length; ApplyTheme(); SaveUserPrefs(); SaveGlobalPrefs(); }
    internal void CycleTheme() => SetTheme(_themeIdx + 1);
    internal void SetPageHome() => SetPage(0);
    internal void SetPageExtract() => SetPage(1);
    internal void SetPageAbout() => SetPage(2);
    internal void SetPageContact() => SetPage(3);
    internal void SetPageSupport() => SetPage(4);
    internal void SetPageSettings() => SetPage(5);

    // ── Language / Theme / Page ──
    private void ApplyLang()
    {
        try
        {
            FlowDirection = (_lang == "fa" || _lang == "ar") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            LblTitle.Text = T("title");
            LblSub.Text = T("subtitle");
            LblCarrierH.Text = T("carrier");
            LblPayloadH.Text = T("payload");
            LblPassH.Text = T("password");
            LblCarrierE.Text = T("carrier");
            LblPassE.Text = T("password");
            LblPayloadHint.Text = T("hintFormats");
            BtnBrowseCH.Content = "📂 " + T("browse");
            BtnBrowseP.Content = "📂 " + T("browse");
            BtnBrowseCE.Content = "📂 " + T("browse");
            BtnGoHide.Content = "💾 " + T("hideBtn");
            BtnGoExtract.Content = "📤 " + T("extractBtn");
            ChkShowH.Content = T("show");
            ChkShowE.Content = T("show");
            LblStatusCap.Text = T("status");
            LblStatus.Text = T("ready");
            LblAboutTitle.Text = T("aboutTitle");
            LblAboutDesc.Text = T("aboutDesc");
            LblFeatures.Text = T("features");
            RunTutorial.Text = T("tutorial");
            LblContactTitle.Text = T("contactTitle");
            LblSupportTitle.Text = T("supportTitle");
            LblSupportStar.Text = T("supportStar");
            BtnStar.Content = T("supportStarBtn");
            LblWalletCap.Text = T("supportWallet");
            BtnCopy.Content = "📋 " + T("supportCopy");
            LblLangCap.Text = T("language");
            RefreshThemeLabels();
            LblDarkGroup.Text = T("darkGroup");
            LblLightGroup.Text = T("lightGroup");
            RefreshAuthTexts();
            RefreshAccountLabels();
            RenderLog();
            string[] keys = { "home", "extract", "about", "contact", "support", "settings" };
            for (int i = 0; i < 6; i++) _navs[i].ToolTip = T(keys[i]);
            UpdateCapacity(FCarrierH.Text, LblCapH);
            UpdateCapacity(FCarrierE.Text, LblCapE);
        }
        catch { /* decoration must never crash the app */ }
    }

    private void PaintThemeSwatches()
    {
        try
        {
            for (int i = 0; i < 9; i++)
            {
                var rd = new ResourceDictionary { Source = new Uri($"Themes/{ThemeFiles[i]}", UriKind.Relative) };
                _themes[i].Background = (Brush)rd["CardBrush"];
                _themes[i].Foreground = (Brush)rd["TextBrush"];
            }
        }
        catch { }
    }

    private void RefreshThemeLabels()
    {
        try
        {
            for (int i = 0; i < 9; i++)
                _themes[i].Content = (i == _themeIdx ? "✔ " : "⬤ ") + ThemeLabel(i);
        }
        catch { }
    }

    private void ApplyTheme()
    {
        try
        {
            var appMerged = Application.Current.Resources.MergedDictionaries;
            var dict = new ResourceDictionary { Source = new Uri($"Themes/{ThemeFiles[_themeIdx]}", UriKind.Relative) };
            if (appMerged.Count == 0) appMerged.Add(dict); else appMerged[0] = dict;
            PaintThemeSwatches();

            var selBg = (Brush)FindResource("SideSelectedBrush");
            var selFg = (Brush)FindResource("SideSelectedIconBrush");
            var idleFg = (Brush)FindResource("SideIconBrush");
            for (int i = 0; i < 6; i++)
            {
                _navs[i].Background = _page == i ? selBg : Brushes.Transparent;
                _navs[i].Foreground = _page == i ? selFg : idleFg;
            }
            var pill = (Brush)FindResource("PillBrush");
            var pillFg = (Brush)FindResource("PillFgBrush");
            var sub = (Brush)FindResource("SubBrush");
            for (int i = 0; i < 5; i++)
            {
                _langs[i].Background = i == _langIdx ? pill : Brushes.Transparent;
                _langs[i].Foreground = i == _langIdx ? pillFg : sub;
            }
            RefreshThemeLabels();
        }
        catch { }
    }

    private void SetPage(int i)
    {
        try
        {
            _page = Math.Max(0, Math.Min(5, i));
            for (int k = 0; k < 6; k++)
                _pages[k].Visibility = k == _page ? Visibility.Visible : Visibility.Collapsed;
            ApplyTheme(); // refresh nav/seg selection colors
        }
        catch { }
    }

    // ── Auth gate: per-user register/login + backup/restore ──
    internal void AuthBypass() // test hook for SelfTest (same assembly only)
    {
        _user = "__selftest";
        AuthOverlay.Visibility = Visibility.Collapsed;
        SidebarCard.IsEnabled = true;
        ContentGrid.IsEnabled = true;
    }

    private void InitAuth()
    {
        try
        {
            _authLoginMode = AuthStore.HasUsers();
            _authRestoreMode = false;
            SidebarCard.IsEnabled = false;
            ContentGrid.IsEnabled = false;
            AuthOverlay.Visibility = Visibility.Visible;
            RefreshAuthTexts();
        }
        catch { }
    }

    private void RefreshAuthTexts()
    {
        try
        {
            DecoPanel.CornerRadius = new CornerRadius(0); // fullscreen: square edges
            LblDecoTitle.Text = _authLoginMode ? T("promoTitle1") : T("promoTitle2");
            LblDecoCap.Text = _authLoginMode ? T("promoDesc1") : T("promoDesc2");
            BtnTabSwitch.Content = _authLoginMode ? T("registerGo") : T("loginGo");
            bool hasGid = GoogleId().Length > 0;
            BtnGoogleSetup.Content = T("googleSetupBtn");
            BtnGoogleSetup.Visibility = hasGid ? Visibility.Collapsed : Visibility.Visible;
            if (_authRestoreMode) LblAuthTitle.Text = T("restoreTitle");
            else
            {
                LblAuthTitle.Text = _authLoginMode ? T("login") : T("register");
                BtnAuthGo.Content = _authLoginMode ? T("loginGo") : T("registerGo");
                BtnAuthForgot.Content = T("forgotPass");
                LblOrCap.Text = T("orContinue");
                BtnAuthForgot.Visibility = _authLoginMode ? Visibility.Visible : Visibility.Collapsed;
                AuthPass2Box.Visibility = _authLoginMode ? Visibility.Collapsed : Visibility.Visible;
            }
            AuthFormBox.Visibility = _authRestoreMode ? Visibility.Collapsed : Visibility.Visible;
            AuthRestoreBox.Visibility = _authRestoreMode ? Visibility.Visible : Visibility.Collapsed;
            BtnAuthPick.Content = T("pickBackup");
            if (string.IsNullOrEmpty(_restoreFile)) LblAuthFile.Text = "";
            LblRecCodeCap.Text = T("recoveryCode");
            LblNewPassCap.Text = T("newPass");
            LblNewPass2Cap.Text = T("confirmPass");
            BtnAuthRestore.Content = T("restoreGo");
            BtnBackToLogin.Content = T("backToLogin");
            LblGoogleStep.Visibility = Visibility.Collapsed;
            LblCodeCap.Text = T("codeCap");
            BtnCopyCode.Content = T("copyCode");
            BtnAuthContinue.Content = T("continueBtn");
            LblAuthErr.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    private void RefreshAccountLabels()
    {
        try
        {
            LblAccountCap.Text = T("account");
            LblAccountName.Text = T("loggedInAs") + (_user ?? "");
            LblExportCodeCap.Text = T("recoveryCode");
            BtnBackupExport.Content = T("exportBackup");
            BtnRotateRecovery.Content = T("newCodeBtn");
            BtnLogout.Content = T("logout");
            BtnHistory.Content = T("history");
            LblBackupHint.Text = T("backupHint");
        }
        catch { }
    }

    private void ShowAuthError(string key)
    {
        try { LblAuthErr.Text = T(key); LblAuthErr.Visibility = Visibility.Visible; } catch { }
    }

    private void CompleteLogin(string username)
    {
        try
        {
            _user = username;
            ClearAuthSecrets();
            var rec = AuthStore.Get(username);
            if (rec != null) { SetLang(rec.Lang); SetTheme(rec.Theme); }
            AuthCodeBox.Visibility = Visibility.Collapsed;
            AuthOverlay.Visibility = Visibility.Collapsed;
            SidebarCard.IsEnabled = true;
            ContentGrid.IsEnabled = true;
            RefreshAccountLabels();
            LblStatus.Text = T("welcome") + username;
            LogMsg("welcome", username);
        }
        catch { }
    }

    private void ClearAuthSecrets()
    {
        try
        {
            FAuthPass.Clear(); FAuthPass2.Clear();
            FAuthNew1.Clear(); FAuthNew2.Clear();
            FAuthRecovery.Clear(); FExportCode.Clear();
        }
        catch { }
    }

    private void BtnAuthGo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string u = FAuthUser.Text.Trim();
            string p = FAuthPass.Password;
            if (u.Length < 2 || p.Length == 0) { ShowAuthError("authFillAll"); return; }
            if (_authLoginMode)
            {
                string lkey = u.ToLowerInvariant();
                if (_loginFails.TryGetValue(lkey, out var st) && st.until > DateTime.Now)
                {
                    int secs = (int)Math.Ceiling((st.until - DateTime.Now).TotalSeconds);
                    try
                    {
                        LblAuthErr.Text = T("lockedOut").Replace("{0}", secs.ToString());
                        LblAuthErr.Visibility = Visibility.Visible;
                    }
                    catch { }
                    return;
                }
                var rec = AuthStore.Verify(u, p);
                if (rec == null)
                {
                    _loginFails.TryGetValue(lkey, out var cur);
                    cur.n++;
                    if (cur.n >= 5) cur.until = DateTime.Now.AddSeconds(30 * (cur.n - 4));
                    _loginFails[lkey] = cur;
                    ShowAuthError(AuthStore.Get(u) == null ? "userNotFound" : "wrongPass");
                    return;
                }
                _loginFails.Remove(lkey);
                CompleteLogin(rec.Username);
            }
            else
            {
                if (p != FAuthPass2.Password) { ShowAuthError("passMismatch"); return; }
                string code;
                try { code = AuthStore.Register(u, p, _langIdx, _themeIdx); }
                catch (AuthException ex) { ShowAuthError(ex.Key); return; }
                _user = u;
                _pendingCode = code;
                LblRecoveryCode.Text = code;
                ClearAuthSecrets();
                LblAuthErr.Visibility = Visibility.Collapsed;
                AuthFormBox.Visibility = Visibility.Collapsed;
                AuthRestoreBox.Visibility = Visibility.Collapsed;
                AuthCodeBox.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private void SetAuthMode(bool login)
    {
        try
        {
            _authLoginMode = login;
            _authRestoreMode = false;
            ClearAuthSecrets();
            AuthCodeBox.Visibility = Visibility.Collapsed;
            RefreshAuthTexts();
        }
        catch { }
    }

    private void BtnTabSwitch_Click(object sender, RoutedEventArgs e) => SetAuthMode(!_authLoginMode);

    private void BtnAuthForgot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _authRestoreMode = true;
            _restoreFile = null;
            LblAuthFile.Text = "";
            ClearAuthSecrets();
            AuthCodeBox.Visibility = Visibility.Collapsed;
            RefreshAuthTexts();
        }
        catch { }
    }

    private void BtnBackToLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _authLoginMode = true;
            _authRestoreMode = false;
            ClearAuthSecrets();
            AuthCodeBox.Visibility = Visibility.Collapsed;
            RefreshAuthTexts();
        }
        catch { }
    }

    private void BtnAuthPick_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "StegaSuite backup (*.stegabak)|*.stegabak|All files (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) == true)
            {
                _restoreFile = dlg.FileName;
                LblAuthFile.Text = System.IO.Path.GetFileName(dlg.FileName);
            }
        }
        catch { }
    }

    private void BtnAuthRestore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_restoreFile) || FAuthRecovery.Text.Trim().Length == 0
                || FAuthNew1.Password.Length == 0) { ShowAuthError("authFillAll"); return; }
            if (FAuthNew1.Password != FAuthNew2.Password) { ShowAuthError("passMismatch"); return; }
            string username;
            try { username = AuthStore.ImportBackup(_restoreFile, FAuthRecovery.Text.Trim(), FAuthNew1.Password); }
            catch (AuthException ex) { ShowAuthError(ex.Key); return; }
            _restoreFile = null;
            LblAuthFile.Text = "";
            ClearAuthSecrets();
            CompleteLogin(username);
            LblStatus.Text = T("restoreDone");
            LogMsg("restoreDone");
        }
        catch { }
    }

    private void BtnCopyCode_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_pendingCode ?? LblRecoveryCode.Text); } catch { }
    }

    // Real Google sign-in (OAuth2 + PKCE, system browser). Without a client ID
    // the button guides the user to create one instead of guessing.
    private bool _googleBusy;

    private async void BtnGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (_googleBusy) return;
        string clientId = GoogleId();
        if (clientId.Length == 0)
        {
            ShowAuthError("needClientId");
            try { BtnGoogleSetup.Visibility = Visibility.Visible; } catch { }
            return;
        }
        if (!clientId.Contains(".apps.googleusercontent.com"))
        {
            ShowAuthError("badClientId");
            return;
        }
        _googleBusy = true;
        try
        {
            BtnGoogle.IsEnabled = false;
            LblAuthErr.Visibility = Visibility.Collapsed;
            LblGoogleStep.Visibility = Visibility.Visible;
            var prog = new Progress<string>(s =>
            {
                try { LblGoogleStep.Text = T("gStep_" + s); } catch { }
            });
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            string secret = GoogleSecret();
            var g = await GoogleAuth.SignInAsync(clientId, "StegaSuite", T("googleOk"), cts.Token, prog,
                secret.Length > 0 ? secret : null);
            string mail = g.Email.Trim().ToLowerInvariant();
            var rec = AuthStore.Get(mail);
            if (rec == null)
            {
                string code = AuthStore.RegisterGoogle(mail, _langIdx, _themeIdx);
                _user = mail;
                _pendingCode = code;
                LblRecoveryCode.Text = code;
                ClearAuthSecrets();
                AuthFormBox.Visibility = Visibility.Collapsed;
                AuthRestoreBox.Visibility = Visibility.Collapsed;
                AuthCodeBox.Visibility = Visibility.Visible;
                return;
            }
            if (!rec.GoogleLinked)
            {
                var ans = MessageBox.Show(this, T("linkGoogleQ"), "Google",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ans == MessageBoxResult.Yes)
                {
                    AuthStore.SetGoogleLinked(mail, true);
                    CompleteLogin(rec.Username);
                }
                return;
            }
            CompleteLogin(rec.Username);
        }
        catch (OperationCanceledException)
        {
            try
            {
                // Either the 20s server timeout or the 3min overall timer: both mean
                // Google's token server never answered -> network guidance, not a code bug.
                LblAuthErr.Text = T("googleError") + T("errNetwork");
                LblAuthErr.Visibility = Visibility.Visible;
            }
            catch { }
        }
        catch (Exception ex)
        {
            try
            {
                string msg = ex.Message ?? "";
                // Raw google code, e.g. "invalid_client :: client_secret is missing."
                string gcode = msg.Contains(" :: ")
                    ? msg.Substring(0, msg.IndexOf(" :: ")).Split(' ').Last() : "";
                // First chars of the ID actually sent (public identifier, also visible
                // in the browser bar) — proves whether the NEW Desktop ID is in use.
                string idTip = clientId.Length <= 12 ? clientId : clientId.Substring(0, 12) + "…";
                string tag = " [" + (gcode.Length > 0 ? gcode + " | " : "") + "id: " + idTip + "]";
                string detail = msg.Contains("client_secret") ? T("errClientType") + tag
                    : msg.Contains("redirect_uri") ? T("errRedirect") + tag
                    : (msg.Contains("access_denied") || msg.Contains("denied")) ? T("errDenied")
                    : msg.Contains("network:") ? T("errNetwork") + " (" + msg + ")"
                    : msg;
                LblAuthErr.Text = T("googleError") + detail;
                LblAuthErr.Visibility = Visibility.Visible;
                try { LogMsg("googleError", msg); } catch { }
            }
            catch { }
        }
        finally
        {
            _googleBusy = false;
            try { BtnGoogle.IsEnabled = true; LblGoogleStep.Visibility = Visibility.Collapsed; } catch { }
        }
    }

    private void BtnGoogleSetup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MessageBox.Show(this, T("googleSetup"), T("googleSetupBtn"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            try
            {
                Process.Start(new ProcessStartInfo("https://console.cloud.google.com/apis/credentials")
                    { UseShellExecute = true });
            }
            catch { }
        }
        catch { }
    }

    private void BtnAuthContinue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AuthCodeBox.Visibility = Visibility.Collapsed;
            var rec = _user != null ? AuthStore.Get(_user) : null;
            if (rec != null && !rec.SeenBackupNotice)
            {
                MessageBox.Show(this, T("firstBackupNotice"), T("exportBackup"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                AuthStore.SetNoticeSeen(_user!);
            }
            if (_user != null) CompleteLogin(_user);
        }
        catch { }
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e) => DoLogout();

    private void DoLogout(string? noticeKey = null)
    {
        try
        {
            _user = null;
            _authLoginMode = true;
            _authRestoreMode = false;
            _restoreFile = null;
            FAuthUser.Clear();
            ClearAuthSecrets();
            LblAuthFile.Text = "";
            AuthCodeBox.Visibility = Visibility.Collapsed;
            RefreshAuthTexts();
            SidebarCard.IsEnabled = false;
            ContentGrid.IsEnabled = false;
            AuthOverlay.Visibility = Visibility.Visible;
            LblStatus.Text = T("ready");
            if (noticeKey != null) ShowAuthError(noticeKey);
        }
        catch { }
    }

    private void AuthEnter_KeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key != Key.Enter) return;
            if (AuthRestoreBox.Visibility == Visibility.Visible)
                BtnAuthRestore_Click(BtnAuthRestore, new RoutedEventArgs());
            else BtnAuthGo_Click(BtnAuthGo, new RoutedEventArgs());
        }
        catch { }
    }

    private void BtnBackupExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_user)) return;
            string code = FExportCode.Text.Trim();
            if (code.Length == 0)
            {
                MessageBox.Show(this, T("authFillAll"), T("exportBackup"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = _user + ".stegabak",
                Filter = "StegaSuite backup (*.stegabak)|*.stegabak",
            };
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    AuthStore.ExportBackup(_user, code, dlg.FileName);
                    LogMsg("backupDone", dlg.FileName);
                    LblStatus.Text = T("backupDone") + System.IO.Path.GetFileName(dlg.FileName);
                    FExportCode.Clear();
                }
                catch (AuthException ex)
                {
                    MessageBox.Show(this, T(ex.Key), T("exportBackup"),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch { }
    }

    private void BtnRotateRecovery_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_user)) return;
            string code = AuthStore.RotateRecovery(_user);
            MessageBox.Show(this, T("codeCap") + "\n\n" + code, T("newCodeBtn"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (AuthException ex)
        {
            MessageBox.Show(this, T(ex.Key), T("newCodeBtn"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_user)) return;
            new HistoryWindow(this, _user) { Owner = this }.ShowDialog();
        }
        catch { }
    }

    private async void CheckUpdates()
    {
        if (_updateChecked) return;
        _updateChecked = true;
        try
        {
            await Task.Delay(3000);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("StegaSuite-Windows/" + AppVersion);
            string json = await http.GetStringAsync(
                "https://api.github.com/repos/Alvandcode/StegaSuite-Windows/releases/latest");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string tag = doc.RootElement.TryGetProperty("tag_name", out var tv)
                ? tv.GetString() ?? "" : "";
            string html = doc.RootElement.TryGetProperty("html_url", out var hv)
                ? hv.GetString() ?? "" : "";
            if (Version.TryParse(tag.TrimStart('v'), out var remote)
                && Version.TryParse(AppVersion, out var local) && remote > local)
            {
                var ans = MessageBox.Show(this, $"StegaSuite {tag}\n\n" + T("updateMsg"),
                    T("updateTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ans == MessageBoxResult.Yes && html.Length > 0)
                    Process.Start(new ProcessStartInfo(html) { UseShellExecute = true });
            }
        }
        catch { }
    }

    // ── Events ──
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string s && int.TryParse(s, out int i)) SetPage(i);
    }
    private void Lang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string s && int.TryParse(s, out int i)) SetLang(i);
    }
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string s && int.TryParse(s, out int i)) SetTheme(i);
    }
    private void ChkShowH_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            bool show = ChkShowH.IsChecked == true;
            FPassHShow.Text = FPassH.Password;
            FPassH.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            FPassHShow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
    }
    private void ChkShowE_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            bool show = ChkShowE.IsChecked == true;
            FPassEShow.Text = FPassE.Password;
            FPassE.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            FPassEShow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
    }
    private void FPassH_Changed(object sender, RoutedEventArgs e)
    {
        try { if (FPassHShow.Text != FPassH.Password) FPassHShow.Text = FPassH.Password; } catch { }
    }
    private void FPassHShow_Changed(object sender, TextChangedEventArgs e)
    {
        try { if (FPassH.Password != FPassHShow.Text) FPassH.Password = FPassHShow.Text; } catch { }
    }
    private void FPassE_Changed(object sender, RoutedEventArgs e)
    {
        try { if (FPassEShow.Text != FPassE.Password) FPassEShow.Text = FPassE.Password; } catch { }
    }
    private void FPassEShow_Changed(object sender, TextChangedEventArgs e)
    {
        try { if (FPassE.Password != FPassEShow.Text) FPassE.Password = FPassEShow.Text; } catch { }
    }
    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
        e.Handled = true;
    }
    private void BtnStar_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(GithubUrl) { UseShellExecute = true }); } catch { }
    }
    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Wallet); LblStatus.Text = T("copied"); LogMsg("copied"); } catch { }
    }

    private void PathBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void PathBox_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && sender is TextBox t)
                t.Text = f[0];
        }
        catch { }
    }
    private void FCarrierH_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCapacity(FCarrierH.Text, LblCapH);
        UpdateHideUsage();
    }
    private void FCarrierE_TextChanged(object sender, TextChangedEventArgs e) => UpdateCapacity(FCarrierE.Text, LblCapE);
    private void FPayload_TextChanged(object sender, TextChangedEventArgs e) => UpdateHideUsage();

    /// <summary>Shows live payload-vs-capacity usage on the home page.</summary>
    private void UpdateHideUsage()
    {
        try
        {
            string c = FCarrierH.Text.Trim(), p = FPayload.Text.Trim();
            if (!File.Exists(c) || !File.Exists(p)) return;
            byte[] carrier = File.ReadAllBytes(c);
            long payloadLen = new FileInfo(p).Length;
            var type = PngSteganography.DetectCarrierType(c, carrier);
            long cap = PngSteganography.CapacityBytesForType(type, carrier);
            if (cap <= 0) return;
            long pct = (payloadLen + 512) * 100 / cap;
            LblCapH.Text = $"{T("type")}: {type}  •  {T("capacity")}: ~{cap / 1024} KB  •  {pct}%";
        }
        catch { }
    }

    // ── Operations ──
    private void PickFile(TextBox target)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog(this) == true) target.Text = dlg.FileName;
        }
        catch { }
    }
    private void BtnBrowseCH_Click(object sender, RoutedEventArgs e) => PickFile(FCarrierH);
    private void BtnBrowseP_Click(object sender, RoutedEventArgs e) => PickFile(FPayload);
    private void BtnBrowseCE_Click(object sender, RoutedEventArgs e) => PickFile(FCarrierE);

    private void UpdateCapacity(string path, System.Windows.Controls.TextBlock label)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { label.Text = ""; return; }
            byte[] data = File.ReadAllBytes(path);
            var type = PngSteganography.DetectCarrierType(path, data);
            long cap = PngSteganography.CapacityBytesForType(type, data);
            label.Text = $"{T("type")}: {type}  •  {T("capacity")}: ~{cap / 1024} KB";
        }
        catch { try { label.Text = ""; } catch { } }
    }

    // Log entries are stored as (time, key, arg) and re-rendered in the CURRENT
    // language, so switching language re-translates the whole console history.
    private readonly List<(string Time, string Key, string Arg)> _logEntries = new();

    private void LogMsg(string key, string arg = "")
    {
        try
        {
            _logEntries.Add((DateTime.Now.ToString("HH:mm:ss"), key, arg));
            if (_logEntries.Count > 200) _logEntries.RemoveRange(0, _logEntries.Count - 200);
            RenderLog();
        }
        catch { }
    }

    private void RenderLog()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var (t, k, a) in _logEntries)
                sb.Append('[').Append(t).Append("] ").Append(T(k)).Append(a).AppendLine();
            TxtLog.Text = sb.ToString();
            TxtLog.ScrollToEnd();
        }
        catch { }
    }

    private void SetBusy(bool busy)
    {
        try
        {
            BtnGoHide.IsEnabled = BtnGoExtract.IsEnabled = !busy;
            foreach (var b in _navs) b.IsEnabled = !busy;
            LblStatus.Text = busy ? T("working") : T("ready");
            try
            {
                StatusDot.Fill = busy
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A))
                    : (Brush)FindResource("AccentBrush");
            }
            catch { }
            Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        }
        catch { }
    }

    private static string ToStName(string orig)
    {
        string dir = Path.GetDirectoryName(orig) ?? "";
        string name = Path.GetFileNameWithoutExtension(orig);
        string ext = Path.GetExtension(orig);
        return Path.Combine(dir, $"{name}(st){ext}");
    }

    private async void BtnGoHide_Click(object sender, RoutedEventArgs e)
    {
        string carrierPath = FCarrierH.Text.Trim();
        string payloadPath = FPayload.Text.Trim();
        string pass = FPassH.Password;
        if (!File.Exists(carrierPath) || !File.Exists(payloadPath))
        {
            MessageBox.Show(this, T("needBoth"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        byte[] carrierBytes, payloadBytes;
        try
        {
            carrierBytes = File.ReadAllBytes(carrierPath);
            payloadBytes = File.ReadAllBytes(payloadPath);
        }
        catch (Exception ex)
        {
            LogMsg("error", ex.Message);
            MessageBox.Show(this, T("error") + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var preType = PngSteganography.DetectCarrierType(carrierPath, carrierBytes);
        if ((long)payloadBytes.Length + 512 > PngSteganography.CapacityBytesForType(preType, carrierBytes))
        {
            MessageBox.Show(this, T("needSpace"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SetBusy(true);
        PbHide.Visibility = Visibility.Visible;
        PbHide.Value = 0;
        var hideProg = new Progress<double>(p =>
        {
            try { PbHide.Value = Math.Max(0, Math.Min(100, p * 100)); } catch { }
        });
        try
        {
            string payloadName = Path.GetFileName(payloadPath);
            string? pw = string.IsNullOrEmpty(pass) ? null : pass;
            byte[] output = await Task.Run(() =>
                preType == CarrierType.PNG || preType == CarrierType.BMP
                    ? PngSteganography.HideImage(carrierBytes, payloadBytes, payloadName, pw, hideProg)
                    : PngSteganography.HideGeneric(carrierBytes, payloadBytes, payloadName, pw, preType, hideProg));

            string suggested = ToStName(carrierPath);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(suggested),
                InitialDirectory = Path.GetDirectoryName(suggested),
                Filter = "All files (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, output);
                LogMsg("doneHide", dlg.FileName);
                LblStatus.Text = T("doneHide") + Path.GetFileName(dlg.FileName);
                HistoryStore.Add(_user, "home", payloadName + " → " + Path.GetFileName(dlg.FileName));
            }
        }
        catch (Exception ex)
        {
                LogMsg("error", ex.Message);
            MessageBox.Show(this, T("error") + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { PbHide.Visibility = Visibility.Collapsed; FPassH.Clear(); SetBusy(false); }
    }

    private async void BtnGoExtract_Click(object sender, RoutedEventArgs e)
    {
        string carrierPath = FCarrierE.Text.Trim();
        string pass = FPassE.Password;
        if (!File.Exists(carrierPath))
        {
            MessageBox.Show(this, T("needCarrier"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SetBusy(true);
        PbExtract.Visibility = Visibility.Visible;
        PbExtract.Value = 0;
        var extProg = new Progress<double>(p =>
        {
            try { PbExtract.Value = Math.Max(0, Math.Min(100, p * 100)); } catch { }
        });
        try
        {
            ExtractResult result = await Task.Run(() =>
            {
                byte[] carrier = File.ReadAllBytes(carrierPath);
                var type = PngSteganography.DetectCarrierType(carrierPath, carrier);
                string? pw = string.IsNullOrEmpty(pass) ? null : pass;
                return PngSteganography.ExtractFromBytes(carrier, pw, type, extProg);
            });

            string dir = Path.GetDirectoryName(carrierPath) ?? "";
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = result.FileName,
                InitialDirectory = dir,
                Filter = "All files (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, result.Bytes);
                LogMsg("doneExtract", dlg.FileName);
                LblStatus.Text = T("doneExtract") + Path.GetFileName(dlg.FileName);
                HistoryStore.Add(_user, "extract",
                    result.FileName + " (" + Path.GetFileName(carrierPath) + ")");
            }
        }
        catch (Exception ex)
        {
                LogMsg("error", ex.Message);
            MessageBox.Show(this, T("error") + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { PbExtract.Visibility = Visibility.Collapsed; FPassE.Clear(); SetBusy(false); }
    }
}
