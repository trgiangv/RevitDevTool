interface Disposable {
  dispose(): void;
}

export class Disposables {
  private readonly items: Disposable[] = [];

  add<T extends Disposable>(item: T): T {
    this.items.push(item);
    return item;
  }

  disposeAll(): void {
    this.items.splice(0).forEach((item) => item.dispose());
  }
}
