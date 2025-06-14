delivery-recipient-examine = Адресовано: { $recipient }, { $job }.
delivery-already-opened-examine = Уже вскрыто.
delivery-earnings-examine = Доставив это, вы заработаете станции ​​[color=yellow]{ $spesos }[/color] кредитов.
delivery-recipient-no-name = Безымянный
delivery-recipient-no-job = Неизвестно

delivery-unlocked-self = Вы разблокировали { $delivery } отпечатком пальца.
delivery-opened-self = Вы вскрываете { $delivery }.

delivery-unlocked-others = {CAPITALIZE($recipient)} разблокировал {$delivery} с {POSS-ADJ($possadj)} отпечатком.
delivery-opened-others = {CAPITALIZE($recipient)} открыл {$delivery}.
delivery-unlock-verb = Разблокировать
delivery-open-verb = Вскрыть
delivery-slice-verb = Вскрыть разрезом
delivery-teleporter-amount-examine =
    { $amount ->
        [one] Содержит [color=yellow]{ $amount }[/color] доставку.
       *[other] Содержит [color=yellow]{ $amount }[/color] доставок.
    }
delivery-teleporter-empty = { $entity } пустой.
delivery-teleporter-empty-verb = Забрать почту
# modifiers
delivery-priority-examine = Это [color=orange]приоритетная { $type }[/color]. У вас осталось [color=orange]{ $time }[/color], чтобы доставить её и получить бонус.
delivery-priority-delivered-examine = Это [color=orange]приоритетная { $type }[/color]. Она была доставлена вовремя.
delivery-priority-expired-examine = Это [color=orange]приоритетная { $type }[/color]. Время на её доставку истекло.
delivery-fragile-examine = Эта [color=red]{ $type } хрупкое[/color]. Доставьте её в целости, чтобы получить бонус.
delivery-fragile-broken-examine = Это [color=red]хрупкая { $type }[/color]. Она выглядит сильно повреждённой.
delivery-bomb-examine = Это [color=purple]бомба { $type }[/color]. О нет.
delivery-bomb-primed-examine = Это [color=purple]бомба { $type }[/color]. Чтение этого текста — не лучшее использование вашего времени.
