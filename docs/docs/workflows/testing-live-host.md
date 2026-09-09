# Testing inside a live host

The testing integrations run against an actual Autodesk process so tests can observe real documents, elements, connectors, and host services.

![Host testing workflow](/images/testing/HostTesting.png)

```text
edit code → build → test runner → host bridge → Autodesk API
```

Supported workflows include NUnit and TUnit host testing, plus the separate `RevitDevTool.PyTest` client for pytest. See the [host-testing product contract](https://github.com/trgiangv/RevitDevTool/blob/develop/docs/product/host-testing.md) and [pytest bridge contract](https://github.com/trgiangv/RevitDevTool/blob/develop/docs/product/pytest-bridge.md) for exact behavior.
