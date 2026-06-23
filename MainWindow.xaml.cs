using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

namespace CS_Launcher
{
    /// <summary>
    /// Главное окно приложения, содержащее поля ввода учётных данных и запуск авторизации.
    /// </summary>
    public partial class MainWindow : Window
    {
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
            string? system = null, login = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("System=")) system = line["System=".Length..];
                else if (line.StartsWith("Login=")) login = line["Login=".Length..];
            }

            // Для корректного восстановления нужны оба значения.
            if (string.IsNullOrEmpty(system) || string.IsNullOrEmpty(login)) return false;

            // Применяем сохранённые значения к полям формы.
            TxtSystem.Text = system;
            TxtLogin.Text = login;
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
                $"Login={TxtLogin.Text.Trim()}"
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

            // Логин обязателен для любого режима авторизации.
            if (string.IsNullOrEmpty(login))
            {
                TxtError.Text = "Заполните поле \"Логин\".";
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
                TxtError.Text = system == "*"
                    ? "Не найдены подходящие системы в Systems.xml."
                    : "Заполните поле \"Система\" или проверьте его значение.";
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

            // Сохраняем значения только если был хотя бы один успешный вход.
            if (successCount > 0)
                SaveSettings();

            // Успех считается достигнутым при наличии хотя бы одного успешного логина.
            if (successCount == 0)
            {
                TxtError.Text = failCount > 1
                    ? $"Не удалось выполнить вход ни для одной системы. Ошибок: {failCount}."
                    : "Не удалось выполнить вход ни для одной системы.";
            }
        }
    }
}