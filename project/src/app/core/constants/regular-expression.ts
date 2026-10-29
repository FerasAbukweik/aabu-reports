export abstract class RegularExpression {
  public static readonly Email = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
  public static readonly StudentId = /^\d{10}$/;
}
