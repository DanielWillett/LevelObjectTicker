# Level Object Ticker
Ticks level objects that have a reset timer lower than configured. Takes config multipliers into account when registering objects.

Supports `rocket reload LevelObjectTicker`.

## Config

```xml
<?xml version="1.0" encoding="utf-8"?>
<LevelObjectTickerConfiguration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <!-- How many seconds between ticks. All objects are ticked in one frame. -->
  <tickSpeedSeconds>0.1</tickSpeedSeconds>
  <!-- Any objects with reset timers ( times configured multiplier ) less than this number will be ticked. -->
  <resetTimeLimitSeconds>60</resetTimeLimitSeconds>
  <!-- Set to true to disable ticking. -->
  <disabled>false</disabled>
  <!-- Set to true to show extra debug logging. -->
  <debugLogging>false</debugLogging>
  <!-- List of object GUIDs to ignore, even if they meet 'resetTimeLimitSeconds'. Takes priority over 'objectWhitelist'. -->
  <objectBlacklist>
    <object>00000000-0000-0000-0000-000000000000</object>
  </objectBlacklist>
  <!-- List of object GUIDs to register, even if they don't meet 'resetTimeLimitSeconds'. 'objectBlacklist' takes priority. -->
  <objectWhitelist>
    <object>00000000-0000-0000-0000-000000000000</object>
  </objectWhitelist>
</LevelObjectTickerConfiguration>
```

**No commands or localization**
