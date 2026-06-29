# ViewX Launcher

## Русский

**ViewX Launcher** — это простое WPF-приложение для запуска авторизации в ViewX / ClearSCADA. Приложение позволяет вводить имя системы, логин и пароль, а затем выполнять вход вручную или по списку систем из `Systems.xml`.

Интерфейс автоматически отображается на русском языке, если русская локаль указана в языковых настройках системы; во всех остальных случаях используется английский.

### Возможности

- Ввод системы, логина и пароля в графической форме.
- Автоматическая подстановка фокуса ввода при запуске.
- Поддержка клавиши **Enter**:
  - в полях **Система** и **Логин** — переход к следующему полю;
  - в поле **Пароль** — запуск авторизации.
- Двуязычный интерфейс: русский при русской системной UI-культуре, английский во всех остальных случаях.
- Сохранение введённых значений в INI-файл рядом с исполняемым файлом.
- Поддержка режима массовой авторизации по `Systems.xml`.
- Режим **Attach to process** для автоматического повторного логина после завершения `SE.Scada.ViewX.exe`.
- Использование одной из двух схем запуска:
  - если поле **Система** заполнено конкретным значением — авторизация выполняется только в эту систему;
  - если поле **Система** пустое или содержит `*` — приложение читает `%ProgramData%\Schneider Electric\ClearSCADA\Systems.xml` и авторизуется по всем подходящим системам.
- Считается, что авторизация успешна, если хотя бы один логон прошёл успешно.
- Сохранение состояния чекбокса `Перезапускать ViewX через` в INI-файл.
- Сохранение настроек при закрытии приложения.
- Поддержка сборки в **монолитный single-file exe**.
- Настроенная иконка приложения.

### Системные требования

- Windows.
- .NET 10 runtime не нужен при публикации в self-contained режиме.
- Доступ к компонентам ViewX / ClearSCADA.

### Файлы конфигурации

#### INI-файл

Рядом с исполняемым файлом создаётся файл с тем же именем, но расширением `.ini`.

Пример:

```ini
System=*
Login=operator
AttachToProcess=True
```

Если поле **Система** пустое, при нажатии **Пуск** приложение автоматически заменяет его на `*` и включает режим чтения списка систем из XML.
Если включён чекбокс **Перезапускать ViewX через**, после успешного логина приложение отслеживает процесс `SE.Scada.ViewX.exe` и повторяет авторизацию после его завершения.

#### Systems.xml

Используется файл:

`%ProgramData%\Schneider Electric\ClearSCADA\Systems.xml`

Из него выбираются элементы `System`, у которых одновременно указаны свойства:

- `type="SCX"`
- `enabled="true"`
- `visibleInViewX="true"`

В качестве имени системы используется атрибут `name`.

### Логика работы

1. Пользователь вводит систему, логин и пароль.
2. Если поле **Система** заполнено:
   - приложение выполняет логон только в указанную систему.
3. Если поле **Система** пустое или содержит `*`:
   - приложение загружает список из `Systems.xml`;
   - для каждой подходящей системы выполняется логон.
4. Если хотя бы один логон успешен:
	  - настройки системы, логина и режима attach сохраняются в `.ini`.
5. Настройки также сохраняются при закрытии приложения.
6. Ошибки отображаются в текстовом поле под кнопкой **Пуск**.

### Управление

- **Система** — имя системы.
- **Логин** — имя пользователя.
- **Пароль** — пароль пользователя.
- **Перезапускать ViewX через** — включает отслеживание завершения `SE.Scada.ViewX.exe` и повторный логон через указанное число секунд.
- **Выход после** — включает автоматический выход после указанного числа минут бездействия.
- **Вход** — запуск авторизации.

### Сборка

#### Публикация single-file exe

Проект настроен на сборку в один исполняемый файл.

Команда публикации:

```powershell
dotnet publish -c Release
```

Результат будет доступен в папке публикации для `win-x64`.

### Иконка приложения

Иконка приложения извлекается из:

`C:\Program Files (x86)\Schneider Electric\ClearSCADA\ClientConfig.exe`

и подключается как иконка итогового `.exe`.

### Технические детали

- UI построен на WPF.
- Основная логика авторизации находится в `MainWindow.xaml.cs`.
- Повторные попытки логина реализованы в `CSLogon.cs`.
- Для чтения XML используется `System.Xml.Linq`.

### Структура проекта

- `MainWindow.xaml` — форма приложения.
- `MainWindow.xaml.cs` — логика UI и запуск авторизации.
- `CSLogon.cs` — выполнение логина в ViewX.
- `ViewX Launcher.csproj` — настройки проекта и публикации.
- `app.ico` — иконка приложения.

### Возможные ошибки

- **Не найдены подходящие системы в Systems.xml**
  - проверьте наличие файла `Systems.xml` и атрибутов нужных систем.
- **Не удалось выполнить вход ни для одной системы**
  - проверьте логин, пароль и доступность сервера.
- **Заполните поле "Логин"**
  - логин обязателен для запуска авторизации.

---

## English

**ViewX Launcher** is a simple WPF application for launching ViewX / ClearSCADA logon sessions. The app lets the user enter a system name, username, and password, then perform a direct logon or logon against a list of systems from `Systems.xml`.

The interface automatically switches to Russian when the system UI language is Russian; in all other cases, English is used.

### Features

- Graphical input fields for system, username, and password.
- Automatic initial focus when the app starts.
- **Enter** key support:
  - in **System** and **Login** fields — moves focus to the next field;
  - in **Password** field — starts the logon process.
- Bilingual interface: Russian when the system UI culture is Russian, English in all other cases.
- Saves entered values to an INI file next to the executable.
- Supports bulk logon mode via `Systems.xml`.
- Two launch modes:
  - if **System** contains a specific value — log on only to that system;
  - if **System** is empty or contains `*` — the app reads `%ProgramData%\Schneider Electric\ClearSCADA\Systems.xml` and logs on to all matching systems.
- Logon is considered successful if at least one attempt succeeds.
- The **Attach to process** option automatically repeats logon when `SE.Scada.ViewX.exe` exits.
- The checkbox state is saved to the INI file and on application close.
- Built as a monolithic single-file executable.
- Custom application icon.

### Requirements

- Windows.
- No .NET 10 runtime required when published as self-contained.
- Access to ViewX / ClearSCADA components.

### Configuration files

#### INI file

The app creates an INI file next to the executable with the same base name and the `.ini` extension.

Example:

```ini
System=*
Login=operator
AttachToProcess=True
```

If the **System** field is empty, pressing **Start** automatically replaces it with `*` and switches the app to XML-based system selection.
If **Attach to process** is enabled, the app monitors `SE.Scada.ViewX.exe` after a successful logon and repeats the logon when the process exits.

#### Systems.xml

The app reads:

`%ProgramData%\Schneider Electric\ClearSCADA\Systems.xml`

It selects `System` elements where all of the following attributes match:

- `type="SCX"`
- `enabled="true"`
- `visibleInViewX="true"`

The system name is taken from the `name` attribute.

### Workflow

1. The user enters system, login, and password.
2. If **System** is filled:
   - the app logs on only to that system.
3. If **System** is empty or set to `*`:
   - the app loads systems from `Systems.xml`;
   - it attempts logon for each matching system.
4. If at least one logon succeeds:
	  - the system, login, and attach mode values are saved to the `.ini` file.
5. Settings are also saved when the app closes.
6. Errors are shown in the text field below the **Start** button.

### Controls

- **System** — system name.
- **Login** — user name.
- **Password** — user password.
- **Restart ViewX after** — monitors `SE.Scada.ViewX.exe` and repeats logon after the specified number of seconds.
- **Exit after** — enables automatic exit after the specified number of minutes of inactivity.
- **Start** — starts the logon process.

### Build

#### Single-file publish

The project is configured to publish as a single executable.

Publish command:

```powershell
dotnet publish -c Release
```

The result is placed in the publish folder for `win-x64`.

### Application icon

The app icon is extracted from:

`C:\Program Files (x86)\Schneider Electric\ClearSCADA\ClientConfig.exe`

and assigned as the executable icon.

### Technical details

- UI is implemented with WPF.
- Main UI and logon flow are in `MainWindow.xaml.cs`.
- Retry-based logon logic is implemented in `CSLogon.cs`.
- XML parsing uses `System.Xml.Linq`.

### Project structure

- `MainWindow.xaml` — application window layout.
- `MainWindow.xaml.cs` — UI logic and logon flow.
- `CSLogon.cs` — ViewX logon helper.
- `ViewX Launcher.csproj` — project and publish settings.
- `app.ico` — application icon.

### Common errors

- **No matching systems found in Systems.xml**
  - verify that the file exists and that matching systems have the required attributes.
- **Failed to log on to any system**
  - check the username, password, and server availability.
- **Please fill in the Login field**
  - login is required to start the process.
