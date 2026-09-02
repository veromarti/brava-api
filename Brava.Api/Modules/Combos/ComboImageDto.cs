namespace Brava.Api.Modules.Combos;

// Response for the combo image upload/link endpoints — a combo has a single
// image slot (unlike a product's gallery), so there's just the resolved URL
// to hand back.
public record ComboImageDto(string ImageUrl);
