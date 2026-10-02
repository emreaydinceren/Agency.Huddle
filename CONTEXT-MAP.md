# Context Map

## Contexts

- [Agency.Huddle](./docs/engineering/CONTEXT.md) — the chat surface: Rooms, Members,
  Messages and the Agents that join them. Vocabulary in
  [`docs/engineering/language.md`](./docs/engineering/language.md).

`Huddle.Acp` is not a second context. It is a reusable ACP client that knows
nothing about chat and deliberately keeps ACP's own vocabulary — see
[Two bounded contexts](./docs/Huddle.EngineeringGuide.md#two-bounded-contexts).
