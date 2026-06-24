using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml.Linq;

namespace CS_Launcher
{
    /// <summary>
    /// Главное окно приложения, содержащее поля ввода учётных данных и запуск авторизации.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Имя процесса ViewX, за завершением которого мы можем следить.
        /// </summary>
        private const string ViewXProcessName = "SE.Scada.ViewX";

        /// <summary>
        /// HWND для помещения окна выше других в Z-order.
        /// </summary>
        private static readonly IntPtr HWND_TOP = new IntPtr(0);

        /// <summary>
        /// Флаг SetWindowPos: не менять размер окна.
        /// </summary>
        private const int SWP_NOSIZE = 0x0001;

        /// <summary>
        /// Флаг SetWindowPos: не менять позицию окна.
        /// </summary>
        private const int SWP_NOMOVE = 0x0002;

        /// <summary>
        /// Флаг SetWindowPos: показать окно, если оно скрыто.
        /// </summary>
        private const int SWP_SHOWWINDOW = 0x0040;

        /// <summary>
        /// Команда ShowWindow для восстановления окна.
        /// </summary>
        private const int SW_RESTORE = 9;

        /// <summary>
        /// Путь к INI-файлу рядом с исполняемым файлом.
        /// Используется для сохранения ранее введённых значений.
        /// </summary>
        private static readonly string IniPath = System.IO.Path.ChangeExtension(
            Environment.ProcessPath ?? AppContext.BaseDirectory, ".ini");

        /// <summary>
        /// Путь к системному XML-файлу ClearSCADA, из которого читается список доступных систем.
        /// </summary>
        private static readonly string SystemsXmlPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Schneider Electric",
            "ClearSCADA",
            "Systems.xml");

        /// <summary>
        /// Источник отмены для фонового ожидания завершения процесса.
        /// </summary>
        private CancellationTokenSource? _attachMonitorCts;

        /// <summary>
        /// Создаёт главное окно и задаёт начальный фокус ввода после загрузки окна.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // После отображения окна восстанавливаем сохранённые данные и выбираем
            // поле, которое должно быть готово к вводу без дополнительных действий пользователя.
            Loaded += (_, _) =>
            {
                bool loaded = LoadSettings();

                // Если данные были восстановлены, пользователь обычно продолжает ввод пароля.
                // Если данные не найдены, курсор сразу ставится в поле системы.
                if (loaded)
                    TxtPassword.Focus();
                else
                    TxtSystem.Focus();
            };

            // При закрытии окна сохраняем актуальное состояние формы независимо от результата логина.
            Closing += (_, _) =>
            {
                StopAttachMonitoring();
                SaveSettings();
            };
        }

        /// <summary>
        /// Загружает ранее сохранённые значения системы и логина из INI-файла.
        /// </summary>
        /// <returns>
        /// <c>true</c>, если оба значения успешно найдены и применены к полям ввода;
        /// иначе <c>false</c>.
        /// </returns>
        private bool LoadSettings()
        {
            // Если файл отсутствует, работать не с чем.
            if (!File.Exists(IniPath)) return false;

            // Читаем файл построчно и ищем нужные пары ключ=значение.
            var lines = File.ReadAllLines(IniPath);
            string? system = null, login = null, attachToProcess = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("System=")) system = line["System=".Length..];
                else if (line.StartsWith("Login=")) login = line["Login=".Length..];
                else if (line.StartsWith("AttachToProcess=")) attachToProcess = line["AttachToProcess=".Length..];
            }

            // Для корректного восстановления нужны оба значения.
            if (string.IsNullOrEmpty(system) || string.IsNullOrEmpty(login)) return false;

            // Применяем сохранённые значения к полям формы.
            TxtSystem.Text = system;
            TxtLogin.Text = login;

            // Восстанавливаем состояние режима attach; если значение отсутствует,
            // считаем режим выключенным.
            ChkAttachToProcess.IsChecked = bool.TryParse(attachToProcess, out bool attachEnabled) && attachEnabled;
            return true;
        }

        /// <summary>
        /// Сохраняет текущие значения системы и логина в INI-файл.
        /// </summary>
        private void SaveSettings()
        {
            // Считываем текущее значение системы из формы.
            string system = TxtSystem.Text.Trim();

            // Если система сейчас пуста, сохраняем последнее значение из уже существующего файла,
            // чтобы не потерять ранее выбранную систему при обновлении только логина.
            if (string.IsNullOrWhiteSpace(system) && File.Exists(IniPath))
            {
                system = File.ReadLines(IniPath)
                    .FirstOrDefault(line => line.StartsWith("System="))?["System=".Length..] ?? string.Empty;
            }

            // Файл хранится в простом формате key=value, по одной паре на строку.
            File.WriteAllLines(IniPath, [
                $"System={system}",
                $"Login={TxtLogin.Text.Trim()}",
                $"AttachToProcess={ChkAttachToProcess.IsChecked == true}"
            ]);
        }

        /// <summary>
        /// Читает ClearSCADA Systems.xml и возвращает список имён систем,
        /// удовлетворяющих требованиям по типу и видимости.
        /// </summary>
        /// <returns>Список значений атрибута <c>name</c> для подходящих систем.</returns>
        private static List<string> GetEligibleSystems()
        {
            // Если системный файл отсутствует, возвращаем пустой список без исключения.
            if (!File.Exists(SystemsXmlPath))
                return [];

            // Загружаем XML целиком, чтобы затем выбрать нужные элементы в дереве документа.
            XDocument document = XDocument.Load(SystemsXmlPath);

            // Отбираем только элементы System с ожидаемым набором атрибутов.
            return document
                .Descendants("System")
                .Where(system => (string?)system.Attribute("type") == "SCX"
                    && (string?)system.Attribute("enabled") == "true"
                    && (string?)system.Attribute("visibleInViewX") == "true")
                // Из отобранных узлов берём только имя системы.
                .Select(system => (string?)system.Attribute("name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .ToList();
        }

        /// <summary>
        /// Обрабатывает нажатие Enter в обычных текстовых полях.
        /// Поведение эквивалентно нажатию Tab: фокус переходит на следующий элемент.
        /// </summary>
        /// <param name="sender">Текущий элемент ввода.</param>
        /// <param name="e">Аргументы клавиатурного события.</param>
        private void TxtField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Не даём системе обрабатывать Enter как обычный ввод символа.
                e.Handled = true;

                // Переводим фокус на следующий контрол в цепочке табуляции.
                ((UIElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        /// <summary>
        /// Обрабатывает нажатие Enter в поле пароля и запускает процедуру авторизации.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы клавиатурного события.</param>
        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter в пароле должен запускать тот же сценарий, что и кнопка «Пуск».
                e.Handled = true;
                BtnStart_Click(sender, e);
            }
        }

        /// <summary>
        /// Запускает авторизацию по одной системе или по списку систем из XML.
        /// Если поле "Система" пустое, оно заменяется на символ <c>*</c> и включается режим XML.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы события нажатия кнопки.</param>
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // Если был активен предыдущий attach-мониторинг, завершаем его,
            // чтобы новое ручное нажатие кнопки не создавало дублирующий цикл.
            StopAttachMonitoring();

            // Стираем текст старой ошибки перед каждой новой попыткой входа.
            TxtError.Text = string.Empty;

            // Читаем текущие значения полей формы.
            string system = TxtSystem.Text.Trim();
            string login = TxtLogin.Text.Trim();
            string password = TxtPassword.Password;

            // Пустое поле системы трактуем как запрос на массовый вход по XML-списку.
            if (string.IsNullOrWhiteSpace(system))
            {
                system = "*";
                TxtSystem.Text = system;
            }

            // Сохраняем текущий снимок полей сразу, чтобы состояние чекбокса
            // и введённые значения не зависели от результата логина.
            SaveSettings();

            // Логин обязателен для любого режима авторизации.
            if (string.IsNullOrEmpty(login))
            {
                ShowError("Заполните поле \"Логин\".");
                return;
            }

            // Если в поле системы стоит *, работаем по XML-списку.
            // Иначе выполняем вход только в конкретную указанную систему.
            List<string> systems = system == "*"
                ? GetEligibleSystems()
                : [system];

            // Если список пуст, дальнейшая авторизация бессмысленна.
            if (systems.Count == 0)
            {
                ShowError(system == "*"
                    ? "Не найдены подходящие системы в Systems.xml."
                    : "Заполните поле \"Система\" или проверьте его значение.");
                return;
            }

            // Один экземпляр ViewX.Application используется для серии попыток входа.
            var viewXApp = new ViewX.Application();
            int successCount = 0;
            int failCount = 0;

            // Выполняем вход по всем выбранным системам.
            foreach (string targetSystem in systems)
            {
                if (CSLogon.LogOn(targetSystem, login, password, viewXApp))
                    successCount++;
                else
                    failCount++;
            }

            // При успешном логоне поднимаем окно ViewX поверх остальных окон, чтобы пользователь мог сразу приступить к работе.
            if (successCount > 0)
            {
                BringViewXWindowToTop();
            }

            // Если пользователь включил режим attach и хотя бы один логон прошёл успешно,
            // начинаем фоновое ожидание завершения процесса ViewX.
            if (successCount > 0 && ChkAttachToProcess.IsChecked == true)
                StartAttachMonitoring();

            // Успех считается достигнутым при наличии хотя бы одного успешного логина.
            if (successCount == 0)
            {
                ShowError(failCount > 1
                    ? $"Не удалось выполнить вход ни для одной системы. Ошибок: {failCount}."
                    : "Не удалось выполнить вход ни для одной системы.");
            }
        }

        /// <summary>
        /// Останавливает текущее фоновое ожидание процесса ViewX, если оно было запущено.
        /// </summary>
        private void StopAttachMonitoring()
        {
            _attachMonitorCts?.Cancel();
            _attachMonitorCts?.Dispose();
            _attachMonitorCts = null;
        }

        /// <summary>
        /// Показывает окно приложения и отображает сообщение об ошибке.
        /// </summary>
        /// <param name="message">Текст сообщения об ошибке.</param>
        private void ShowError(string message)
        {
            RestoreAndActivateWindow();

            TxtError.Text = message;
        }

        /// <summary>
        /// Восстанавливает окно приложения и пытается перевести его в foreground.
        /// </summary>
        private void RestoreAndActivateWindow()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Show();
            Activate();

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                _ = ShowWindow(handle, SW_RESTORE);
                _ = SetForegroundWindow(handle);
            }

            Topmost = true;
            Topmost = false;
        }

        /// <summary>
        /// Переводит окно приложения на задний план после успешного логина.
        /// </summary>
        private void MinimizeToBackground()
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Перемещает окно ViewX на вершину Z-order после успешного логина.
        /// </summary>
        private void BringViewXWindowToTop()
        {
            Process? process = FindCurrentUserViewXProcess();
            if (process is null)
                return;

            IntPtr handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
                return;

            // SetWindowPos(HWND_TOP, ...) поднимает окно поверх остальных без изменения размера и позиции.
            _ = SetWindowPos(handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Немедленно отключает мониторинг процесса при снятии чекбокса Attach to process.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы Routed-события.</param>
        private void ChkAttachToProcess_Unchecked(object sender, RoutedEventArgs e)
        {
            StopAttachMonitoring();
        }

        /// <summary>
        /// Запускает фоновое ожидание завершения процесса ViewX и после его закрытия
        /// инициирует повторный логон с текущими настройками формы.
        /// </summary>
        private void StartAttachMonitoring()
        {
            StopAttachMonitoring();

            _attachMonitorCts = new CancellationTokenSource();
            CancellationToken token = _attachMonitorCts.Token;

            _ = Task.Run(() => MonitorViewXProcessAsync(token), token);
        }

        /// <summary>
        /// Ожидает появления и завершения процесса ViewX в фоновом потоке.
        /// </summary>
        /// <param name="token">Токен отмены, который прерывает ожидание при новом запуске авторизации.</param>
        private async Task MonitorViewXProcessAsync(CancellationToken token)
        {
            try
            {
                using Process? process = await WaitForViewXProcessAsync(token).ConfigureAwait(false);

                if (process is null)
                    return;

                // WaitForExit не требует повышения привилегий для процесса того же пользователя/сеанса.
                process.WaitForExit();

                if (token.IsCancellationRequested)
                    return;

                // После завершения процесса возвращаемся в UI-поток и повторяем логон.
                await Dispatcher.InvokeAsync(() => BtnStart_Click(this, new RoutedEventArgs()));
            }
            catch (OperationCanceledException)
            {
                // Нормальный сценарий при новом ручном запуске авторизации.
            }
            catch (InvalidOperationException)
            {
                // Процесс мог завершиться между поиском и началом ожидания.
            }
        }

        /// <summary>
        /// Ищет процесс ViewX, запущенный в текущем сеансе пользователя.
        /// Если процесс ещё не появился, продолжает ждать до отмены.
        /// </summary>
        /// <param name="token">Токен отмены ожидания.</param>
        /// <returns>Объект процесса или <c>null</c>, если ожидание было отменено.</returns>
        private static async Task<Process?> WaitForViewXProcessAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                Process? process = FindCurrentUserViewXProcess();
                if (process is not null)
                    return process;

                await Task.Delay(500, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Находит экземпляр процесса ViewX в текущем сеансе пользователя.
        /// </summary>
        /// <returns>Подходящий процесс или <c>null</c>, если процесс не найден.</returns>
        private static Process? FindCurrentUserViewXProcess()
        {
            int currentSessionId = Process.GetCurrentProcess().SessionId;

            foreach (Process process in Process.GetProcessesByName(ViewXProcessName))
            {
                try
                {
                    if (process.SessionId == currentSessionId)
                        return process;
                }
                catch (InvalidOperationException)
                {
                    // Процесс мог завершиться в момент проверки.
                }
            }

            return null;
        }

        /// <summary>
        /// Перемещает указанное окно в Z-order с помощью Win32 SetWindowPos.
        /// </summary>
        /// <param name="hWnd">Дескриптор окна.</param>
        /// <param name="hWndInsertAfter">Позиция в Z-order, например HWND_TOP.</param>
        /// <param name="x">Координата X (игнорируется при SWP_NOMOVE).</param>
        /// <param name="y">Координата Y (игнорируется при SWP_NOMOVE).</param>
        /// <param name="cx">Ширина (игнорируется при SWP_NOSIZE).</param>
        /// <param name="cy">Высота (игнорируется при SWP_NOSIZE).</param>
        /// <param name="uFlags">Комбинация флагов SetWindowPos.</param>
        /// <returns><c>true</c>, если операция выполнена успешно; иначе <c>false</c>.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        /// <summary>
        /// Восстанавливает окно из свёрнутого состояния.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Переводит окно в foreground.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}