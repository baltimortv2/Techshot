---
inclusion: always
---
## Code Style Guidelines
1. **Naming**: Всегда называй приватные поля с `_underscore` (например, `private float _speed;`).
2. **Performance**: Никогда не используй `GetComponent` или `Find` внутри метода `Update`. Кешируй их в `Awake`.
3. **LINQ**: Не используй LINQ в методах, вызываемых каждый кадр (избегай GC Alloc).
4. **Attributes**: Если поле приватное, но нужно в инспекторе — используй `[SerializeField]`.