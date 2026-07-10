export class History<T> {
  private readonly past: T[] = [];
  private readonly future: T[] = [];

  public constructor(private readonly limit = 30) {}

  public commit(previousState: T): void {
    this.past.push(previousState);
    if (this.past.length > this.limit) this.past.shift();
    this.future.length = 0;
  }

  public undo(currentState: T): T | undefined {
    const previous = this.past.pop();
    if (previous === undefined) return undefined;
    this.future.push(currentState);
    return previous;
  }

  public redo(currentState: T): T | undefined {
    const next = this.future.pop();
    if (next === undefined) return undefined;
    this.past.push(currentState);
    return next;
  }

  public get canUndo(): boolean {
    return this.past.length > 0;
  }

  public get canRedo(): boolean {
    return this.future.length > 0;
  }
}
