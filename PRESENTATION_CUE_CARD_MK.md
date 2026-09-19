# RebelAI - presentation cue card

## Поделба

- **Презентер 1:** проблем → public site → reservation → code.
- **Презентер 2:** notification → approve → Tonight → Floor → architecture.

## Пред старт

- Public Home е отворен во првиот browser.
- Admin Dashboard е отворен и manager-от е најавен во вториот browser.
- Bookings, Tonight и Floor се отворени како резервни табови.
- Demo име: `Demo Bowie`.
- Терминот е валиден и најмалку 2 часа во иднина.
- Нема приватни notifications или credentials на екран.

## Презентер 1 - 4 минути

### 1. Отворање

> RebelAI го поврзува едноставното искуство на гостинот со реалната оперативна работа на персоналот.

### 2. Home / Menu / Events

> Јавниот дел го задржува Rebel идентитетот и му дава на посетителот директен пристап до менито, настаните и резервацијата.

### 3. Book a Table

- Избери валиден датум и време.
- Внеси 2 или 4 гости.
- Внеси `Demo Bowie`, demo телефон и кратка note.
- Кликни `Request the table`.

> Формата е намерно кратка. Не бараме email; гостинот добива reservation code.

### 4. Confirmation

> Барањето е Pending. Со кодот гостинот може да ја провери состојбата или да ја откаже кога правилата го дозволуваат тоа.

### Handoff

> За гостинот процесот завршува тука. Барањето веќе пристигна кај персоналот, па сега ќе видиме што се случува зад сцената.

## Презентер 2 - 6 минути

### 1. Dashboard / notification

> Dashboard прво ги покажува работите што бараат реакција: нови барања, недоделени маси и следни пристигнувања.

Ако нема live badge, пребарај `Demo Bowie` или code. Не чекај.

### 2. Details / approval

- Отвори ја demo резервацијата.
- Покажи детали и activity history.
- Approve и додели слободна маса.

> Workflow-от користи јасни статуси: Pending, Approved, Arrived, No-show, Rejected и Cancelled.

### 3. Tonight

> Tonight е оперативен поглед за смената: review requests, expected arrivals, staff coverage, floor и настани.

### 4. Floor

- Покажи `Preview space`.
- Префрли на `Edit plan`.
- Помести само една demo маса.
- Не бриши floor.

> Floor plan-от ја претвора листата со маси во разбирлив простор. Edit режимот дозволува движење, resize, типови, седишта и fixtures, со autosave.

### 5. Architecture

> Апликацијата е ASP.NET Core MVC на .NET 8, со Entity Framework Core, PostgreSQL, Identity roles и SignalR за live notifications. Решението е поделено на Domain, Application, Infrastructure, Web и Tests.

### Заклучок

> RebelAI не е само веб-страница. Една едноставна guest акција веднаш станува организирана работа за персоналот.

## Ако нешто откаже

- Нема notification: отвори Bookings и пребарај име/code.
- Validation error: поправи го полето и објасни дека business rule-от работи.
- Нема маса: покажи дека резервацијата останува во Needs attention.
- Апликацијата падна: продолжи со backup screenshots.
- Нема време: прескокни Planner, Staff и Events; задржи reservation → approval → Floor.

## Пет кратки Q&A одговори

- **Зошто нема email?** Помал friction и помалку непотребни лични податоци; code овозможува lookup.
- **Како admin дознава?** Database notification плус SignalR live event.
- **Како се штити admin?** Identity authentication, role policies, anti-forgery и server validation.
- **Дали floor е слика?** Не, елементите се data-backed и layout-от се autosave-ира.
- **Каде е AI?** Скриен е со feature flag за оваа презентација; core operations е намерниот demo scope.
